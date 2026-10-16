using System.Runtime.InteropServices;
using System.Buffers.Binary;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Dafeiyu;

namespace DeskPet.Desktop;

internal sealed record ClipSpec(string File, int Columns, int Frames, double Fps, bool Loop, int Width = 480, int Height = 520, int Top = 0);

internal sealed class PetSkin
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Prompt { get; init; }
    public string? Folder { get; init; }
    public bool BuiltIn => Folder == null;
    public string? Atlas { get; init; }
    public Dictionary<string, ClipSpec> Clips { get; } = new();
    public ClipSpec? Gaze { get; set; }
    public override string ToString() => Name;
}

internal sealed class SpriteAssets : IDisposable
{
    private readonly Dictionary<string, Bitmap> images = new();
    private readonly Dictionary<(string, int), byte[]> masks = new();
    public static readonly string[] Interactions = { "lifted", "landing", "petting", "poked", "feeding", "happy", "sleeping", "special" };
    public static Stream OpenAsset(string path) => AssetLoader.Open(new Uri("avares://DeskPet/Assets/" + path.Replace('\\', '/')));
    public static readonly string[] PairedActions = { "lifted", "landing", "feeding", "happy", "sleeping", "special", "special2", "stumble" };
    public static List<PetSkin> LoadSkins(string data)
    {
        var skins = new List<PetSkin>();
        foreach (var item in new[] { ("e-bao", "E包", "e-bao-spritesheet.png", PersonaProfiles.Ebao), ("dafeiyu", "大肥鱼", "spritesheet.png", PersonaProfiles.Dafeiyu), ("marfu", "马尔芙", "marfu-spritesheet.png", PersonaProfiles.Marfu) })
            skins.Add(new PetSkin { Id = item.Item1, Name = item.Item2, Atlas = item.Item3, Prompt = item.Item4 });
        using (var source = OpenAsset("bundled-skins/xiaobei/pet.json")) skins.Add(ReadManifest(source, "xiaobei", null, "bundled-skins/xiaobei/"));
        var root = Path.Combine(data, "skins");
        if (Directory.Exists(root)) foreach (var folder in Directory.GetDirectories(root))
        {
            if (Path.GetFileName(folder).StartsWith(".import-", StringComparison.Ordinal)) continue;
            try { using var source = File.OpenRead(Path.Combine(folder, "pet.json")); var skin = ReadManifest(source, Path.GetFileName(folder), folder); ValidateSkin(skin); skins.Add(skin); }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException or ArgumentException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException) { System.Diagnostics.Trace.WriteLine("Skin load: " + e.GetType().Name); }
        }
        return skins;
    }
    private static PetSkin ReadManifest(Stream stream, string id, string? folder, string prefix = "")
    {
        if (stream.Length > 65536) throw new InvalidDataException("外观说明文件过大。");
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        var version = root.GetProperty("version").GetInt32();
        var name = root.GetProperty("name").GetString() ?? "自定义伙伴";
        if (name.Length is < 1 or > 60) throw new InvalidDataException("外观名称应为 1～60 字。");
        var prompt = root.TryGetProperty("system_prompt", out var p) ? p.GetString() ?? "" : PersonaProfiles.Generic(name);
        if (prompt.Length > 8000) throw new InvalidDataException("人格最多 8000 字。");
        var skin = new PetSkin { Id = id, Name = name, Folder = folder, Prompt = prompt, Atlas = version == 1 ? root.GetProperty("atlas").GetString() : null };
        if (version == 1) return skin;
        if (version != 2 || root.GetProperty("cell_width").GetInt32() != 480 || root.GetProperty("cell_height").GetInt32() != 520) throw new InvalidDataException("外观需要 v1 或 480 × 520 的 v2 格式。");
        foreach (var property in root.GetProperty("animations").EnumerateObject())
        {
            var a = property.Value;
            var columns = a.GetProperty("columns").GetInt32(); var frames = a.GetProperty("frames").GetInt32(); var fps = a.GetProperty("fps").GetDouble();
            if (columns is < 1 or > 16 || frames is < 1 or > 64 || !double.IsFinite(fps) || fps is <= 0 or > 60) throw new InvalidDataException("外观帧数或速度无效。");
            skin.Clips[property.Name] = new ClipSpec(prefix + a.GetProperty("file").GetString(), columns, frames, fps, a.GetProperty("loop").GetBoolean());
        }
        foreach (var required in AnimationPlayer.States.Concat(Interactions)) if (!skin.Clips.ContainsKey(required)) throw new InvalidDataException("缺少动作：" + required);
        var gaze = root.GetProperty("gaze");
        if (gaze.GetProperty("columns").GetInt32() != 4 || gaze.GetProperty("frames").GetInt32() != 16) throw new InvalidDataException("视线图需要 4 列、16 帧。");
        skin.Gaze = new ClipSpec(prefix + gaze.GetProperty("file").GetString(), 4, 16, 8, true);
        return skin;
    }
    public static string SafePath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("外观资源路径无效。");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("外观资源目录不接受符号链接。");
        var target = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(fullRoot, StringComparison.Ordinal)) throw new InvalidDataException("外观资源不能越过所在目录。");
        var current = target;
        while (current.Length >= fullRoot.Length)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("外观资源不接受符号链接。");
            current = Path.GetDirectoryName(current)!;
        }
        return target;
    }
    private static void ValidateSkin(PetSkin skin)
    {
        using var assets = new SpriteAssets();
        if (skin.Atlas != null)
        {
            var image = assets.Get(skin, skin.Atlas);
            var size = image.PixelSize;
            if (size.Width % 8 != 0 || size.Height % 11 != 0 || size.Width / 8 is < 64 or > 768 || size.Height / 11 is < 64 or > 832 || (long)size.Width * size.Height > 55000000) throw new InvalidDataException("精灵图需要 8 列 × 11 行透明 PNG。");
            for (int row = 0; row < 11; row++) for (int col = 0; col < (row < 9 ? AnimationPlayer.Counts[row] : 8); col++) assets.ValidateFrame(skin, new ClipSpec(skin.Atlas, 8, 88, 8, true, size.Width / 8, size.Height / 11), row * 8 + col);
        }
        else foreach (var spec in skin.Clips.Values.Append(skin.Gaze!))
        {
            var image = assets.Get(skin, spec.File);
            if (image.PixelSize.Width != spec.Columns * spec.Width || image.PixelSize.Height != (int)Math.Ceiling((double)spec.Frames / spec.Columns) * spec.Height) throw new InvalidDataException("外观图像尺寸与说明不一致：" + spec.File);
            for (int i = 0; i < spec.Frames; i++) assets.ValidateFrame(skin, spec, i);
        }
    }
    public static PetSkin Import(string manifestPath, string data)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        if (new FileInfo(manifestPath).Length > 65536) throw new InvalidDataException("外观说明文件过大。");
        var id = "skin-" + Guid.NewGuid().ToString("N");
        using var stream = File.OpenRead(manifestPath);
        var skin = ReadManifest(stream, id, root);
        ValidateSkin(skin);
        var staging = Path.Combine(data, "skins", ".import-" + Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(data, "skins", id);
        Directory.CreateDirectory(staging);
        try
        {
            var files = skin.Atlas != null ? new[] { skin.Atlas } : skin.Clips.Values.Append(skin.Gaze!).Select(c => c.File).Distinct();
            long total = 0;
            foreach (var relative in files)
            {
                var source = SafePath(root, relative); total += new FileInfo(source).Length;
                if (total > 256L * 1024 * 1024) throw new InvalidDataException("外观资源总大小不能超过 256 MB。");
                var target = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            File.Copy(manifestPath, Path.Combine(staging, "pet.json"));
            using (var copied = File.OpenRead(Path.Combine(staging, "pet.json"))) ValidateSkin(ReadManifest(copied, id, staging));
            Directory.Move(staging, destination);
            using var final = File.OpenRead(Path.Combine(destination, "pet.json")); return ReadManifest(final, id, destination);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    public Bitmap Get(PetSkin skin, string path)
    {
        var key = skin.Id + "/" + path;
        if (!images.TryGetValue(key, out var image))
        {
            using var stream = skin.BuiltIn ? OpenAsset(path) : File.OpenRead(SafePath(skin.Folder!, path));
            if (stream.Length > 64L * 1024 * 1024) throw new InvalidDataException("单张外观图不能超过 64 MB。");
            Span<byte> header = stackalloc byte[24]; stream.ReadExactly(header); stream.Position = 0;
            if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("外观图像必须是 PNG。");
            var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]); var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
            if (width == 0 || height == 0 || width > 8192 || height > 33280 || (long)width * height > 55000000) throw new InvalidDataException("外观 PNG 尺寸过大或无效。");
            image = new Bitmap(stream); images[key] = image;
        }
        return image;
    }
    public ClipSpec Resolve(PetSkin skin, string action, bool original)
    {
        if (skin.BuiltIn && PairedActions.Contains(action))
        {
            if (!original) return new ClipSpec($"companion-animations/{skin.Id}/{action}.png", 8, 8, 8, action is "sleeping" or "lifted");
            if (action is "special2" or "stumble") return new ClipSpec($"additional/{skin.Id}.png", 4, 8, 8, false, Top: action == "stumble" ? 0 : 1040);
        }
        if (skin.Clips.TryGetValue(action, out var manifest)) return manifest with { Fps = Interactions.Contains(action) ? 8 : manifest.Fps };
        if (Interactions.Contains(action) && skin.BuiltIn) return new ClipSpec($"{skin.Id}-interactions.png", 4, 4, 8, action is "sleeping" or "lifted", Top: Array.IndexOf(Interactions, action) * 520);
        var row = Array.IndexOf(AnimationPlayer.States, action); if (row < 0) row = 0;
        if (skin.BuiltIn && skin.Id is "dafeiyu" or "marfu" && row == 2) row = 1;
        var atlas = Get(skin, skin.Atlas!); var w = atlas.PixelSize.Width / 8; var h = atlas.PixelSize.Height / 11;
        return new ClipSpec(skin.Atlas!, 8, AnimationPlayer.Counts[row], 8, action is "idle" or "running" or "running-right" or "running-left", w, h, row * h);
    }
    public ClipSpec Gaze(PetSkin skin)
    {
        if (skin.Gaze != null) return skin.Gaze;
        var atlas = Get(skin, skin.Atlas!); return new ClipSpec(skin.Atlas!, 8, 16, 8, true, atlas.PixelSize.Width / 8, atlas.PixelSize.Height / 11, atlas.PixelSize.Height / 11 * 9);
    }
    public static PixelRect Rect(ClipSpec spec, int frame) => new(frame % spec.Columns * spec.Width, spec.Top + frame / spec.Columns * spec.Height, spec.Width, spec.Height);
    private byte[] Mask(PetSkin skin, ClipSpec spec, int frame)
    {
        var key = (skin.Id + "/" + spec.File + "/" + spec.Top, frame);
        if (masks.TryGetValue(key, out var result)) return result;
        var bytes = new byte[spec.Width * spec.Height * 4];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { Get(skin, spec.File).CopyPixels(Rect(spec, frame), handle.AddrOfPinnedObject(), bytes.Length, spec.Width * 4); }
        finally { handle.Free(); }
        result = new byte[spec.Width * spec.Height];
        for (int i = 0; i < result.Length; i++) result[i] = bytes[i * 4 + 3];
        masks[key] = result; return result;
    }
    private void ValidateFrame(PetSkin skin, ClipSpec spec, int frame)
    {
        var mask = Mask(skin, spec, frame);
        if (!mask.Any(b => b > 16) || !mask.Any(b => b == 0)) throw new InvalidDataException("外观每帧需要可见人物和透明背景。");
    }
    public bool Hit(PetSkin skin, ClipSpec spec, int frame, double x, double y)
    {
        var px = (int)(x * spec.Width); var py = (int)(y * spec.Height);
        return px >= 0 && py >= 0 && px < spec.Width && py < spec.Height && Mask(skin, spec, frame)[py * spec.Width + px] > 16;
    }
    public void Dispose() { foreach (var image in images.Values) image.Dispose(); images.Clear(); masks.Clear(); }
}

internal sealed class SpriteView : Control
{
    public required SpriteAssets Assets { get; init; }
    public required PetSkin Skin { get; set; }
    public new ClipSpec? Clip { get; set; }
    public int Frame { get; set; }
    public bool MirrorX { get; set; }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Clip == null) return;
        var r = SpriteAssets.Rect(Clip, Frame);
        using var transform = context.PushTransform(MirrorX ? new Matrix(-1, 0, 0, 1, Bounds.Width, 0) : Matrix.Identity);
        context.DrawImage(Assets.Get(Skin, Clip.File), new Rect(r.X, r.Y, r.Width, r.Height), new Rect(Bounds.Size));
    }
    public bool Hits(Point point) => Clip != null && Bounds.Width > 0 && Bounds.Height > 0 && Assets.Hit(Skin, Clip, Frame, MirrorX ? 1 - point.X / Bounds.Width : point.X / Bounds.Width, point.Y / Bounds.Height);
}
