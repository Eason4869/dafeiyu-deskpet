#import <AppKit/AppKit.h>
#import <AVFoundation/AVFoundation.h>
#import <Carbon/Carbon.h>
#import <Security/Security.h>
#import <ServiceManagement/ServiceManagement.h>
#include <math.h>
#include <string.h>

// This library has no initializers that request permissions or open audio devices.
static void MainSync(dispatch_block_t block) {
    if ([NSThread isMainThread]) block(); else dispatch_sync(dispatch_get_main_queue(), block);
}

int dp_keychain_key(uint8_t *bytes, int length) {
    @autoreleasepool {
        if (!bytes || length != 32) return errSecParam;
        NSMutableDictionary *query = [@{(__bridge id)kSecClass:(__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService:@"com.deskpet.desktop.data-key.v1",
            (__bridge id)kSecAttrAccount:@"data-encryption-key",
            (__bridge id)kSecReturnData:@YES,
            (__bridge id)kSecMatchLimit:(__bridge id)kSecMatchLimitOne} mutableCopy];
        CFTypeRef result = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        if (status == errSecItemNotFound) {
            uint8_t generated[32]; status = SecRandomCopyBytes(kSecRandomDefault, sizeof(generated), generated);
            if (status != errSecSuccess) return status;
            [query removeObjectForKey:(__bridge id)kSecReturnData];
            [query removeObjectForKey:(__bridge id)kSecMatchLimit];
            query[(__bridge id)kSecValueData] = [NSData dataWithBytes:generated length:32];
            query[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
            status = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
            memset_s(generated, sizeof(generated), 0, sizeof(generated));
            if (status != errSecSuccess && status != errSecDuplicateItem) return status;
            // A concurrent process may have won creation. Always read the stored winner.
            [query removeObjectForKey:(__bridge id)kSecValueData];
            [query removeObjectForKey:(__bridge id)kSecAttrAccessible];
            query[(__bridge id)kSecReturnData] = @YES;
            query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
            status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        }
        if (status != errSecSuccess) { if (result) CFRelease(result); return status; }
        NSData *data = CFBridgingRelease(result);
        if (data.length != 32) return errSecDecode;
        memcpy(bytes, data.bytes, 32); return 0;
    }
}

int dp_microphone_status(void) { return (int)[AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeAudio]; }
void dp_microphone_request(void (*completion)(int)) {
    [AVCaptureDevice requestAccessForMediaType:AVMediaTypeAudio completionHandler:^(BOOL granted) { completion(granted ? 1 : 0); }];
}

@interface DPMicrophone : NSObject
@property(strong) AVAudioEngine *engine;
@property(strong) AVAudioConverter *converter;
@property(strong) NSLock *lock;
@property(strong) NSMutableData *pending;
@property(strong) id observer;
@property BOOL failed;
@end
@implementation DPMicrophone
- (instancetype)init { if ((self = [super init])) { _lock = [NSLock new]; _pending = [NSMutableData new]; } return self; }
@end

void *dp_microphone_start(int *error) {
    @autoreleasepool { @try {
        if (dp_microphone_status() != AVAuthorizationStatusAuthorized) { *error = -1; return NULL; }
        DPMicrophone *mic = [DPMicrophone new]; mic.engine = [AVAudioEngine new];
        AVAudioInputNode *input = mic.engine.inputNode;
        AVAudioFormat *source = [input outputFormatForBus:0];
        if (source.channelCount == 0 || source.sampleRate == 0) { *error = -2; return NULL; }
        AVAudioFormat *target = [[AVAudioFormat alloc] initWithCommonFormat:AVAudioPCMFormatInt16 sampleRate:16000 channels:1 interleaved:NO];
        mic.converter = [[AVAudioConverter alloc] initFromFormat:source toFormat:target];
        if (!mic.converter) { *error = -3; return NULL; }
        __weak DPMicrophone *weakMic = mic;
        mic.observer = [[NSNotificationCenter defaultCenter] addObserverForName:AVAudioEngineConfigurationChangeNotification object:mic.engine queue:nil usingBlock:^(NSNotification *note) {
            DPMicrophone *strongMic = weakMic; [strongMic.lock lock]; strongMic.failed = YES; [strongMic.lock unlock];
        }];
        [input installTapOnBus:0 bufferSize:2048 format:source block:^(AVAudioPCMBuffer *buffer, AVAudioTime *time) {
            DPMicrophone *strongMic = weakMic; if (!strongMic) return;
            @autoreleasepool {
                AVAudioFrameCount capacity = (AVAudioFrameCount)ceil(buffer.frameLength * 16000.0 / source.sampleRate) + 32;
                AVAudioPCMBuffer *output = [[AVAudioPCMBuffer alloc] initWithPCMFormat:target frameCapacity:capacity];
                __block BOOL supplied = NO; NSError *conversionError = nil;
                AVAudioConverterOutputStatus status = [strongMic.converter convertToBuffer:output error:&conversionError withInputFromBlock:^AVAudioBuffer *(AVAudioPacketCount count, AVAudioConverterInputStatus *state) {
                    if (supplied) { *state = AVAudioConverterInputStatus_NoDataNow; return nil; }
                    supplied = YES; *state = AVAudioConverterInputStatus_HaveData; return buffer;
                }];
                [strongMic.lock lock];
                if (status == AVAudioConverterOutputStatus_Error || conversionError) strongMic.failed = YES;
                else if (strongMic.pending.length + output.frameLength * 2 > 16000 * 2 * 5) strongMic.failed = YES;
                else if (output.frameLength) [strongMic.pending appendBytes:output.int16ChannelData[0] length:output.frameLength * 2];
                [strongMic.lock unlock];
            }
        }];
        NSError *startError = nil; [mic.engine prepare];
        if (![mic.engine startAndReturnError:&startError]) {
            [input removeTapOnBus:0]; [[NSNotificationCenter defaultCenter] removeObserver:mic.observer];
            *error = (int)(startError.code ?: -4); return NULL;
        }
        *error = 0; return (__bridge_retained void *)mic;
    } @catch (NSException *exception) { *error = -5; return NULL; } }
}
int dp_microphone_read(void *handle, uint8_t *data, int capacity) {
    @autoreleasepool {
        DPMicrophone *mic = (__bridge DPMicrophone *)handle;
        if (!mic || !data || capacity < 2) return -1;
        [mic.lock lock];
        if (mic.failed || !mic.engine.isRunning || dp_microphone_status() != AVAuthorizationStatusAuthorized) { [mic.lock unlock]; return -1; }
        NSUInteger count = MIN(mic.pending.length, (NSUInteger)(capacity & ~1));
        if (count) { memcpy(data, mic.pending.bytes, count); [mic.pending replaceBytesInRange:NSMakeRange(0, count) withBytes:NULL length:0]; }
        [mic.lock unlock]; return (int)count;
    }
}
void dp_microphone_stop(void *handle) {
    if (!handle) return;
    @autoreleasepool { DPMicrophone *mic = CFBridgingRelease(handle);
        [mic.engine stop]; [mic.engine.inputNode removeTapOnBus:0];
        if (mic.observer) [[NSNotificationCenter defaultCenter] removeObserver:mic.observer];
        [mic.lock lock]; [mic.pending resetBytesInRange:NSMakeRange(0, mic.pending.length)]; [mic.pending setLength:0]; [mic.lock unlock];
    }
}

@interface DPOutput : NSObject
@property(strong) AVAudioEngine *engine;
@property(strong) AVAudioPlayerNode *player;
@property(strong) AVAudioFormat *format;
@property(strong) NSLock *lock;
@property int64_t pending;
@property NSUInteger generation;
@end
@implementation DPOutput
@end
void *dp_output_start(int rate, int *error) {
    @autoreleasepool { @try {
        if (rate != 16000 && rate != 24000 && rate != 48000) { *error = -1; return NULL; }
        DPOutput *output = [DPOutput new]; output.lock = [NSLock new]; output.engine = [AVAudioEngine new]; output.player = [AVAudioPlayerNode new];
        // AVAudioEngine mixer uses Float32; incoming mono Int16 is converted explicitly below.
        output.format = [[AVAudioFormat alloc] initWithCommonFormat:AVAudioPCMFormatFloat32 sampleRate:rate channels:1 interleaved:NO];
        [output.engine attachNode:output.player]; [output.engine connect:output.player to:output.engine.mainMixerNode format:output.format];
        NSError *failure = nil; [output.engine prepare];
        if (![output.engine startAndReturnError:&failure]) { *error = (int)(failure.code ?: -2); return NULL; }
        [output.player play]; *error = 0; return (__bridge_retained void *)output;
    } @catch (NSException *exception) { *error = -3; return NULL; } }
}
int dp_output_enqueue(void *handle, const uint8_t *data, int length) {
    @autoreleasepool { @try {
        DPOutput *output = (__bridge DPOutput *)handle;
        if (!output || !output.engine.isRunning || length < 2 || length % 2) return -1;
        AVAudioPCMBuffer *buffer = [[AVAudioPCMBuffer alloc] initWithPCMFormat:output.format frameCapacity:length / 2]; buffer.frameLength = length / 2;
        float *samples = buffer.floatChannelData[0];
        for (int i = 0; i < length / 2; ++i) { int16_t sample = (int16_t)((uint16_t)data[i * 2] | ((uint16_t)data[i * 2 + 1] << 8)); samples[i] = sample / 32768.0f; }
        [output.lock lock]; output.pending += length; NSUInteger generation = output.generation; [output.lock unlock];
        __weak DPOutput *weakOutput = output;
        [output.player scheduleBuffer:buffer completionCallbackType:AVAudioPlayerNodeCompletionDataPlayedBack completionHandler:^(AVAudioPlayerNodeCompletionCallbackType type) {
            DPOutput *strongOutput = weakOutput; if (!strongOutput) return;
            [strongOutput.lock lock]; if (generation == strongOutput.generation) strongOutput.pending = MAX(0, strongOutput.pending - length); [strongOutput.lock unlock];
        }];
        return 0;
    } @catch (NSException *exception) { return -2; } }
}
int64_t dp_output_pending(void *handle) {
    DPOutput *output = (__bridge DPOutput *)handle; if (!output || !output.engine.isRunning) return -1;
    [output.lock lock]; int64_t result = output.pending; [output.lock unlock]; return result;
}
void dp_output_volume(void *handle, float volume) { DPOutput *output = (__bridge DPOutput *)handle; output.player.volume = volume; }
void dp_output_interrupt(void *handle) {
    DPOutput *output = (__bridge DPOutput *)handle;
    [output.lock lock]; output.generation++; output.pending = 0; [output.lock unlock];
    [output.player stop]; [output.player play];
}
void dp_output_stop(void *handle) { if (!handle) return; @autoreleasepool { DPOutput *output = CFBridgingRelease(handle); [output.player stop]; [output.engine stop]; } }

@interface DPWave : NSObject <AVAudioPlayerDelegate>
@property(strong) AVAudioPlayer *player;
@property(atomic) int status;
@end
@implementation DPWave
- (void)audioPlayerDidFinishPlaying:(AVAudioPlayer *)player successfully:(BOOL)flag { self.status = flag ? 0 : -1; }
- (void)audioPlayerDecodeErrorDidOccur:(AVAudioPlayer *)player error:(NSError *)error { self.status = -1; }
@end
void *dp_wave_start(const uint8_t *bytes, int length, float volume) {
    @autoreleasepool { @try {
        if (!bytes || length < 1) return NULL;
        DPWave *wave = [DPWave new]; NSError *error = nil;
        wave.player = [[AVAudioPlayer alloc] initWithData:[NSData dataWithBytes:bytes length:length] error:&error];
        if (!wave.player || error) return NULL;
        wave.player.delegate = wave; wave.player.volume = volume; wave.status = 1;
        if (![wave.player prepareToPlay] || ![wave.player play]) return NULL;
        return (__bridge_retained void *)wave;
    } @catch (NSException *exception) { return NULL; } }
}
int dp_wave_status(void *handle) {
    DPWave *wave = (__bridge DPWave *)handle;
    if (!wave || wave.status < 0) return -1;
    if (wave.player.isPlaying) return 1;
    // Poll as well: a managed pool thread need not run an NSRunLoop for delegates.
    return wave.status == 0 || wave.player.currentTime >= wave.player.duration - 0.1 ? 0 : -1;
}
void dp_wave_volume(void *handle, float volume) { DPWave *wave = (__bridge DPWave *)handle; wave.player.volume = volume; }
void dp_wave_stop(void *handle) { if (!handle) return; @autoreleasepool { DPWave *wave = CFBridgingRelease(handle); [wave.player stop]; wave.player.delegate = nil; } }

double dp_idle_seconds(void) { return CGEventSourceSecondsSinceLastEventType(kCGEventSourceStateCombinedSessionState, kCGAnyInputEventType); }
void dp_pointer(double *x, double *y) { CGEventRef event = CGEventCreate(NULL); CGPoint point = event ? CGEventGetLocation(event) : CGPointZero; if (event) CFRelease(event); *x = point.x; *y = point.y; }
typedef struct { double x,y,width,height,workX,workY,workWidth,workHeight,scale; } DPDisplay;
int dp_display_count(void) { __block int count; MainSync(^{ count = (int)NSScreen.screens.count; }); return count; }
int dp_display(int index, DPDisplay *result) {
    __block int found = 0;
    MainSync(^{ NSArray<NSScreen *> *screens = NSScreen.screens;
        if (index < 0 || index >= (int)screens.count) return;
        NSScreen *screen = screens[index]; NSRect frame = screen.frame, work = screen.visibleFrame;
        double top = NSMaxY(screens[0].frame);
        *result = (DPDisplay){frame.origin.x,top-NSMaxY(frame),frame.size.width,frame.size.height,work.origin.x,top-NSMaxY(work),work.size.width,work.size.height,screen.backingScaleFactor}; found = 1;
    }); return found;
}
int dp_window(void *handle, int topmost, int ignoreMouse, int allSpaces) {
    __block int result = 0;
    MainSync(^{ id object = (__bridge id)handle; if (![object isKindOfClass:[NSWindow class]]) return;
        NSWindow *window = object; window.level = topmost ? NSFloatingWindowLevel : NSNormalWindowLevel; window.ignoresMouseEvents = ignoreMouse != 0;
        NSWindowCollectionBehavior flags = window.collectionBehavior;
        flags &= ~(NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorMoveToActiveSpace | NSWindowCollectionBehaviorFullScreenAuxiliary);
        if (allSpaces) flags |= NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary;
        window.collectionBehavior = flags; result = 1;
    }); return result;
}
int dp_fullscreen_front(void) {
    @autoreleasepool {
        pid_t front = NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier;
        NSArray *windows = CFBridgingRelease(CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly | kCGWindowListExcludeDesktopElements, kCGNullWindowID));
        for (NSDictionary *window in windows) {
            if ([window[(__bridge id)kCGWindowOwnerPID] intValue] != front || [window[(__bridge id)kCGWindowLayer] intValue] != 0) continue;
            CGRect bounds; if (!CGRectMakeWithDictionaryRepresentation((__bridge CFDictionaryRef)window[(__bridge id)kCGWindowBounds], &bounds)) continue;
            uint32_t count = 0; CGGetActiveDisplayList(0, NULL, &count);
            CGDirectDisplayID displays[32]; count = MIN(count, 32u); CGGetActiveDisplayList(count, displays, &count);
            for (uint32_t i = 0; i < count; ++i) { CGRect screen = CGDisplayBounds(displays[i]);
                if (fabs(bounds.origin.x-screen.origin.x)<2 && fabs(bounds.origin.y-screen.origin.y)<2 && fabs(bounds.size.width-screen.size.width)<2 && fabs(bounds.size.height-screen.size.height)<2) return 1;
            }
        } return 0;
    }
}

typedef struct { EventHotKeyRef hotkey; EventHandlerRef handler; void (*callback)(void); UInt32 identifier; } DPHotKey;
static OSStatus HotKeyEvent(EventHandlerCallRef next, EventRef event, void *context) {
    DPHotKey *hotkey = context; EventHotKeyID identifier;
    if (GetEventParameter(event, kEventParamDirectObject, typeEventHotKeyID, NULL, sizeof(identifier), NULL, &identifier) == noErr && identifier.signature == 'DPet' && identifier.id == hotkey->identifier) { hotkey->callback(); return noErr; }
    return eventNotHandledErr;
}
void *dp_hotkey_register(uint32_t key, uint32_t modifiers, void (*callback)(void), int *error) {
    __block DPHotKey *hotkey = NULL;
    MainSync(^{
        static UInt32 nextId = 1;
        DPHotKey *entry = calloc(1, sizeof(DPHotKey)); entry->callback = callback; entry->identifier = nextId++;
        EventTypeSpec type = {kEventClassKeyboard, kEventHotKeyPressed};
        *error = InstallApplicationEventHandler(HotKeyEvent, 1, &type, entry, &entry->handler);
        if (*error == noErr) *error = RegisterEventHotKey(key, modifiers, (EventHotKeyID){'DPet', entry->identifier}, GetApplicationEventTarget(), 0, &entry->hotkey);
        if (*error != noErr) { if (entry->handler) RemoveEventHandler(entry->handler); free(entry); }
        else hotkey = entry;
    }); return hotkey;
}
void dp_hotkey_unregister(void *handle) { if (!handle) return; MainSync(^{ DPHotKey *entry = handle; UnregisterEventHotKey(entry->hotkey); RemoveEventHandler(entry->handler); free(entry); }); }
int dp_login_status(void) { __block int status; MainSync(^{ status = (int)SMAppService.mainAppService.status; }); return status; }
int dp_login_set(int enabled) {
    __block int result = 0;
    MainSync(^{ NSError *error = nil; BOOL ok = enabled ? [SMAppService.mainAppService registerAndReturnError:&error] : [SMAppService.mainAppService unregisterAndReturnError:&error];
        if (!ok) result = (int)(error.code ?: -1);
    }); return result;
}
