using System;
namespace Dafeiyu
{
    public static class PersonaProfiles
    {
        public const string Dafeiyu="你是大肥鱼，一位温和、俏皮的蓝白鲸鱼娘桌面伙伴。用自然的中文与用户对话，帮助写代码、做设计和整理思路。回答清楚简洁，如实表达不确定性。你不能直接看见用户桌面，也不会自动读取文件。";
        public const string Ebao="你是E包，一位傲娇、毒舌的白发正太桌面伙伴。用自然的中文与用户对话，帮助写代码、做设计和整理思路。回答清楚简洁，如实表达不确定性。你不能直接看见用户桌面，也不会自动读取文件。";
        public const string Marfu="你是马尔芙，一位金发绿眼的魔法少女桌面伙伴，角色背景是《哈利波特》中德拉科·马尔福的女性版本（性转马尔福）。你聪明、自信、俏皮，带一点马尔福式的骄傲和嘴硬，也愿意耐心陪伴用户。用自然的中文与用户聊天，帮助写代码、做设计和整理思路。可以偶尔用简短的魔法比喻，但不要让角色扮演妨碍清楚实用的回答。不挖苦用户，不编造能力，遇到不确定的信息如实说明。你不能直接看见用户桌面，也不会自动读取文件。";
        public static string Generic(string name) { return "你是"+name+"，一位温和、俏皮的桌面伙伴。用自然的中文与用户对话，帮助写代码、做设计和整理思路。回答清楚简洁，如实表达不确定性。你不能直接看见用户桌面，也不会自动读取文件。"; }
        public static void Activate(PetSettings settings,SkinDefinition skin)
        {
            settings.Normalize();
            if(settings.PersonaProfileVersion==0)
            {
                string old=settings.SystemPrompt ?? "";
                if(!String.IsNullOrWhiteSpace(old) && old!=Generic("{pet_name}"))
                {
                    string owner=old.Contains("鲸鱼") || old.Contains("大肥鱼") ? "dafeiyu" : settings.SkinId;
                    if(!settings.PersonaOverrides.ContainsKey(owner)) settings.PersonaOverrides[owner]=old;
                }
                settings.PersonaProfileVersion=1;
            }
            string prompt; settings.SystemPrompt=settings.PersonaOverrides.TryGetValue(skin.Id,out prompt) ? prompt : skin.SystemPrompt;
            settings.SkinId=skin.Id;
        }
        public static void Save(PetSettings settings,SkinDefinition skin,string prompt)
        {
            prompt=(prompt ?? "").Trim(); if(prompt.Length==0 || prompt.Length>8000) throw new InvalidOperationException("人格应为 1～8000 个字符。");
            Activate(settings,skin); settings.PersonaOverrides[skin.Id]=prompt; settings.SystemPrompt=prompt;
        }
        public static void Reset(PetSettings settings,SkinDefinition skin) { Activate(settings,skin); settings.PersonaOverrides.Remove(skin.Id); settings.SystemPrompt=skin.SystemPrompt; }
    }
}
