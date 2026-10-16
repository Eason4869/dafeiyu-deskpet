using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Dafeiyu
{
    // Bundled companions never enter data/skins, which belongs to user imports.
    internal static class BundledSkinCache
    {
        const string Prefix="Dafeiyu.Bundled.Xiaobei.";
        sealed class Entry
        {
            public string Relative,Resource;
            public long Length;
            public byte[] Hash;
        }
        internal static SkinDefinition LoadXiaobei(string dataDirectory)
        {
            var assembly=Assembly.GetExecutingAssembly();SkinManifest manifest;
            using(var stream=assembly.GetManifestResourceStream(Prefix+"pet.json")) {
                // Older/test builds without the optional bundle retain their existing catalog.
                if(stream==null)return null;
                manifest=SkinCatalog.ReadManifest(stream);
            }
            if(manifest.Version!=2 || manifest.Animations==null || manifest.Gaze==null)
                throw new InvalidOperationException("内置小贝外观说明不完整。");
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"pet.json",manifest.Preview,manifest.Gaze.File};
            foreach(var clip in manifest.Animations.Values) {
                if(clip==null)throw new InvalidOperationException("内置小贝动作说明不完整。");
                paths.Add(clip.File);
            }
            var entries=new List<Entry>();var resourceNames=new HashSet<string>(StringComparer.Ordinal);long total=0;
            foreach(string relative in paths.OrderBy(p=>p,StringComparer.Ordinal)) {
                CheckRelative(relative);
                string resource=Prefix+relative.Replace('/','.');
                if(!resourceNames.Add(resource))throw new InvalidOperationException("内置外观资源名称冲突。");
                using(var stream=assembly.GetManifestResourceStream(resource)) {
                    if(stream==null)throw new InvalidOperationException("内置小贝缺少素材："+relative);
                    long max=relative=="pet.json" ? 65536 : 25L*1024*1024;
                    total+=stream.Length;
                    if(stream.Length>max || total>256L*1024*1024)throw new InvalidOperationException("内置外观素材过大。");
                    using(var sha=SHA256.Create())entries.Add(new Entry{Relative=relative,Resource=resource,Length=stream.Length,Hash=sha.ComputeHash(stream)});
                }
            }
            string version;
            using(var bytes=new MemoryStream()) {
                using(var writer=new BinaryWriter(bytes,Encoding.UTF8,true))foreach(var entry in entries){writer.Write(entry.Relative);writer.Write(entry.Length);writer.Write(entry.Hash);}
                bytes.Position=0;using(var sha=SHA256.Create())version=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            }
            string root=Path.GetFullPath(Path.Combine(dataDirectory,"bundled-skins"));
            Directory.CreateDirectory(root);CheckDirectory(root);
            // Keep paths short for .NET Framework installations; full hashes still verify every file.
            string name="xiaobei-"+version.Substring(0,24),destination=Path.Combine(root,name);
            // A damaged cache is left intact. A verified repair is reused on later launches.
            var candidates=new List<string>{destination};
            candidates.AddRange(Directory.GetDirectories(root,name+"-repair-*").OrderBy(p=>p,StringComparer.Ordinal));
            foreach(string candidate in candidates)if(Matches(candidate,entries)) {
                IllustratedPack.Validate(candidate,manifest,false);
                return new SkinDefinition("xiaobei",candidate,manifest){Bundled=true};
            }
            string staging=Path.Combine(root,".staging-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
            try {
                foreach(var entry in entries) {
                    string target=Path.Combine(staging,entry.Relative.Replace('/',Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using(var input=assembly.GetManifestResourceStream(entry.Resource))using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
                }
                IllustratedPack.Validate(staging,manifest,true);
                if(Directory.Exists(destination))destination+="-repair-"+Guid.NewGuid().ToString("N");
                try {Directory.Move(staging,destination);}
                catch(IOException) {
                    // Concurrent first launches may have published the same immutable bundle.
                    if(!Matches(destination,entries))throw;
                }
                return new SkinDefinition("xiaobei",destination,manifest){Bundled=true};
            } finally {
                // Only this invocation's new, contained staging directory can be removed.
                if(Directory.Exists(staging))Directory.Delete(staging,true);
            }
        }
        static void CheckRelative(string relative)
        {
            if(String.IsNullOrWhiteSpace(relative) || relative.Length>200 || relative.IndexOfAny(new[]{'\\',':','\0'})>=0 || relative.StartsWith("/") ||
                (relative!="pet.json" && !relative.EndsWith(".png",StringComparison.OrdinalIgnoreCase)) ||
                relative.Split('/').Any(p=>p.Length==0 || p=="." || p==".." || p.EndsWith(".") || p.EndsWith(" ") || p.IndexOfAny(Path.GetInvalidFileNameChars())>=0))
                throw new InvalidOperationException("内置外观素材路径无效。");
        }
        static void CheckDirectory(string path)
        {if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("内置外观缓存目录不能是链接。");}
        static bool Matches(string folder,IEnumerable<Entry> entries)
        {
            if(!Directory.Exists(folder))return false;
            try {
                CheckDirectory(folder);
                foreach(var entry in entries) {
                    string path=entry.Relative=="pet.json" ? Path.Combine(folder,"pet.json") : IllustratedPack.AssetPath(folder,entry.Relative);
                    if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
                    using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())
                        if(stream.Length!=entry.Length || !sha.ComputeHash(stream).SequenceEqual(entry.Hash))return false;
                }
                return true;
            } catch(Exception e) {
                if(!(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException))throw;
                return false;
            }
        }
    }
}
