# Mac 平台服务

目标为 macOS 14+ / .NET 10，引用 `DeskPet.Core`。Windows 只执行托管加密与边界测试，不加载 `libDeskPetMac.dylib`。

1. Mac runner 在 publish **之前**执行 `bash desktop/DeskPet.Platform.Mac/native/build.sh arm64` 或 `x86_64`。脚本只生成一个目标架构，`libDeskPetMac.dylib` 自动复制到应用发布目录，随后由发布脚本签名。
2. 应用启动时先 `MacServices.InstallSecretProtection()`，再打开 Settings、HistoryStore、ConversationQueue。数据目录 `~/Library/Application Support/DeskPet`，日志目录 `~/Library/Logs/DeskPet`，分别支持 `DESKPET_DATA_DIR` / `DESKPET_LOG_DIR` 隔离覆盖。
3. 用户点击听写或 Live 后才 `await MacServices.RequestMicrophoneAccessAsync()`；返回 false 时显示权限说明并保留文字功能。`CreateRecorder` / `CreateMicrophone` 的 `Start()` 不会隐式弹权限框；设备缺失/撤销/无数据有明确异常或 `Failed` 事件，结束和失败时释放 AVAudioEngine。录音单次最多 60 秒、16kHz 单声道 Int16 PCM，`FinishAsync()` 返回 WAV。
4. `CreateLiveOutput` 接受 16/24/48kHz 单声道 Int16 PCM，支持打断、音量、音量包络和错误事件；120 秒队列上限，10 秒无播放进展报警。`CreateSpeech(ApiClient)` 使用原云端 Speech API 或系统 `/usr/bin/say`，私密文本走 stdin，不进入命令行。云语音在内存播放，没有明文临时 WAV。
5. `RegisterHotKey` 返回的 `IDisposable` 必须一直持有并在关闭时释放。参数是 macOS 虚拟键码与 Carbon 修饰键位，不是 Windows 键码；同应用重复绑定及 Carbon 报告的独占冲突会抛出明确错误，系统/其他应用的所有快捷键冲突不能保证全部可检测。回调在原生 UI 事件线程执行，调用者应快速返回。
6. `ConfigureWindow` 接收 `NSWindow` 或已附着窗口的 `NSView` 指针。`GetPointerRelativeToWindow` 直接返回内容左上原点的逻辑点，处理 Retina 与 flipped view，推荐用于透明像素命中和视线。`GetPointerPosition` 和 `GetDisplays` 使用 Quartz 全局**逻辑点**，主屏左上原点，允许负坐标；Avalonia 的物理像素坐标需要按对应显示器缩放换算。显示器清单实时读取，调用者在屏幕变化后重新约束位置。
7. `SetLoginAtStartup` 仅由用户主动选择时调用，使用 `SMAppService.mainAppService`，必须在完整 `.app` 中运行。状态 0 未注册、1 启用、2 待用户批准、3 未找到。没有 LaunchAgent shell 命令或固定应用路径。

加密格式为 `DPM1` + 12-byte nonce + 16-byte GCM tag + 密文；版本头参与认证。32-byte 密钥位于当前用户默认文件型 Keychain 的 generic-password 项 `com.deskpet.desktop.data-key.v1` / `data-encryption-key`，查询限定同一钥匙串，使用创建应用的默认 ACL，不设置允许所有应用访问的 ACL，不会写文件、参数或日志。Keychain 不可用、未授权或密钥损坏时失败，不退化成明文，不覆盖损坏密钥。Windows DPAPI 数据不能在 Mac 解密，迁移需源设备导出或重新输入。无 Developer ID 的预览升级可能需要重新批准 Keychain 访问，不能承诺正式签名后的授权连续性。

托管检查：`dotnet run --project desktop/DeskPet.Platform.Mac/Tests/DeskPet.Platform.Mac.Tests.csproj`。Mac 上构建原生库后再运行同命令并追加 `-- --native-smoke`，可验证原生 ABI、权限状态查询、鼠标、空闲、显示器与登录状态读取；此测试不申请麦克风权限，不修改登录项。

原生 CI 检查：`bash desktop/DeskPet.Platform.Mac/native/self-test.sh arm64`，Intel 改为 `x86_64`。脚本调用原构建脚本，再 clang 构建 `native/build/native_library_self_test`，动态链接真正的桥接库并运行（60 秒超时）。检查内容包括临时隔离 Keychain 的创建/重复读取/锁定拒绝/重开持久性/损坏拒绝、NSWindow/NSView 及相对坐标、Spaces 标志、快捷键注册/重复冲突/释放、空闲/显示器、无 .app 时登录修改拒绝、未授权麦克风拒绝及音频格式/句柄错误。不会调用麦克风权限请求，不录音，不修改生产 Keychain 或登录项。自检进程暂时禁用 Keychain 交互，结束时恢复搜索列表并删除临时钥匙串；密钥和临时密码从不进入参数或日志。

自检同时尝试零音量的真实输出图与 WAV 播放完成回调；机器没有可用输出设备时明确 `SKIP hardware output` 并验证错误路径，不能视为硬件音频通过。原生自检产物和 dylib 已在本目录 `.gitignore` 排除。

Windows 托管测试覆盖认证加密、篡改/错误密钥/外来格式拒绝、非 Mac 原生拦截、录音生命周期及真实子进程取消回收；录音和朗读进程用可注入传输边界测试，不伪造 Mac 实机成功。真实麦克风首次授权/拒绝/撤销/设备切换、Live 双向听感、系统朗读听感、快捷键跨应用触发、登录项重启、真实多屏/Spaces、睡眠唤醒、签名后 Keychain 持续访问仍需在两架构 Mac 手动验收。前台全屏检测是无需屏幕录制授权的公开 CGWindow 查询，系统可能隐藏跨应用数据；不能据此宣称完整全屏识别保证。系统 say 逐句启动（长句最多 180 字符），每句使用当前音量；修改音量从下一句生效，当前句通过“停止”立即取消。云语音和 Live 音量实时生效。

原生接口依据：[AVAudioConverter](https://developer.apple.com/documentation/avfaudio/avaudioconverter/convert%28to%3Aerror%3Awithinputfrom%3A%29?language=objc)、[Keychain](https://developer.apple.com/documentation/security/secitemcopymatching%28_%3A_%3A%29)、[SMAppService](https://developer.apple.com/documentation/ServiceManagement/SMAppService?language=objc)。
