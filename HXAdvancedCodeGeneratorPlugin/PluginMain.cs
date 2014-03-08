using ASCompletion;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using PluginCore;
using PluginCore.Controls;
using PluginCore.Helpers;
using PluginCore.Localization;
using PluginCore.Managers;
using PluginCore.Utilities;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

namespace HXADCodeGeneratorPlugin
{
    public class PluginMain : IPlugin
    {
        private string pluginName = "HXADCodeGeneratorPlugin";
        private string pluginGuid = "92f41ee5-6d96-4f03-95a5-b46610fe5c2e";
        private string pluginHelp = "www.flashdevelop.org/community/";
        private string pluginDesc = "Haxe advanced code generator for the ASCompletion engine.";
        private string pluginAuth = "FlashDevelop Team";
        private Settings settingObject;
        private string settingFilename;

        private const string methodPattern = @"function\s+[a-z_0-9.]+\s*\(";
        private const string classPattern = @"class\s+[a-zA-Z_0-9.]+\s*";

        #region Required Properties

        /// <summary>
        /// Api level of the plugin
        /// </summary>
        public int Api { get { return 1; } }

        /// <summary>
        /// Name of the plugin
        /// </summary>
        public string Name { get { return pluginName; } }

        /// <summary>
        /// GUID of the plugin
        /// </summary>
        public string Guid { get { return pluginGuid; } }

        /// <summary>
        /// Author of the plugin
        /// </summary>
        public string Author { get { return pluginAuth; } }

        /// <summary>
        /// Description of the plugin
        /// </summary>
        public string Description { get { return pluginDesc; } }

        /// <summary>
        /// Web address for help
        /// </summary>
        public string Help { get { return pluginHelp; } }

        /// <summary>
        /// Object that contains the settings
        /// </summary>
        [Browsable(false)]
        public object Settings { get { return settingObject; } }

        #endregion

        #region Required Methods

        /// <summary>
        /// Initializes the plugin
        /// </summary>
        public void Initialize()
        {
            InitBasics();
            LoadSettings();
            AddEventHandlers();
        }

        /// <summary>
        /// Disposes the plugin
        /// </summary>
        public void Dispose()
        {
            SaveSettings();
        }

        /// <summary>
        /// Handles the incoming events
        /// </summary>
        public void HandleEvent(object sender, NotifyEvent e, HandlingPriority prority)
        {
            switch (e.Type)
            {
                case EventType.Command:
                    DataEvent de = (DataEvent)e;
                    switch (de.Action)
                    {
                        case "ASCompletion.ContextualGenerator":
                            e.Handled = ASContext.HasContext && ASContext.Context.IsFileValid && ContextualGenerator(ASContext.CurSciControl);
                            break;
                    }
                    break;
            }
        }

        #endregion

        #region Custom Methods

        /// <summary>
        /// Initializes important variables
        /// </summary>
        public void InitBasics()
        {
            string dataPath = Path.Combine(PathHelper.DataDir, "HXADCodeGenerator");
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
            this.settingFilename = Path.Combine(dataPath, "Settings.fdb");
            this.pluginDesc = TextHelper.GetString("Info.Description");
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary>
        public void AddEventHandlers()
        {
            EventManager.AddEventHandler(this, EventType.UIStarted | EventType.Command);
        }

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        public void LoadSettings()
        {
            settingObject = new Settings();
            if (!File.Exists(settingFilename)) SaveSettings();
            else settingObject = (Settings)ObjectSerializer.Deserialize(settingFilename, settingObject);
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        private void SaveSettings()
        {
            ObjectSerializer.Serialize(settingFilename, settingObject);
        }

        #endregion
        
        public static void GenerateJob(GeneratorJobType job, MemberModel member, ClassModel inClass, string itemLabel, object data)
        {
            ScintillaNet.ScintillaControl Sci = ASContext.CurSciControl;
            switch (job)
            {
                case GeneratorJobType.MakeClassFinal:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeMemberFinal(Sci, inClass, classPattern);
                    }
                    finally
                    {
                        Sci.EndUndoAction();
                    }
                    break;
                case GeneratorJobType.MakeMethodFinal:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeMemberFinal(Sci, member, methodPattern);
                    }
                    finally
                    {
                        Sci.EndUndoAction();
                    }
                    break;
                case GeneratorJobType.MakeClassNotFinal:
                case GeneratorJobType.MakeMethodNotFinal:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeMemberNotFinal(Sci, inClass ?? member);
                    }
                    finally
                    {
                        Sci.EndUndoAction();
                    }
                    break;
                case GeneratorJobType.MakeClassExtern:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeClassExtern(Sci, inClass);
                    }
                    finally
                    {
                        Sci.EndUndoAction();
                    }
                    break;
                case GeneratorJobType.MakeClassNotExtern:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeClassNotExtern(Sci, inClass);
                    }
                    finally
                    {
                        Sci.EndUndoAction();
                    }
                    break;
                case GeneratorJobType.AddStaticModifier:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeMemberNotFinal(Sci, member);
                        AddStaticModifier(Sci, member, methodPattern);
                    }
                    finally
                    {
                        Sci.EndUndoAction();
                    }
                    break;
            }
        }
        
        private static bool ContextualGenerator(ScintillaNet.ScintillaControl Sci)
        {
            int position = Sci.CurrentPos;
            int line = Sci.LineFromPosition(position);
            string text = Sci.GetLine(line);
            FoundDeclaration found = GetDeclarationAtLine(Sci, line);
            if (!GetDeclarationIsValid(Sci, found)) return false;
            if (found.member == null && found.inClass != ClassModel.VoidClass)
            {
                ShowChangeClass(found);
                return true;
            }
            if ((found.member.Flags & (FlagType.Static | FlagType.Constructor)) == 0 && (found.member.Flags & FlagType.Function) > 0)
            {
                ShowChangeMethod(found);
                return true;
            }
            return false;
        }

        private static FoundDeclaration GetDeclarationAtLine(ScintillaNet.ScintillaControl Sci, int line)
        {
            FoundDeclaration result = new FoundDeclaration();
            foreach (ClassModel aClass in ASContext.Context.CurrentModel.Classes)
            {
                if (aClass.LineFrom > line || aClass.LineTo < line) continue;
                result.inClass = aClass;
                foreach (MemberModel member in aClass.Members)
                {
                    if (member.LineFrom > line || member.LineTo < line) continue;
                    result.member = member;
                    return result;
                }
                return result;
            }
            return result;
        }

        private static bool GetDeclarationIsValid(ScintillaNet.ScintillaControl Sci, FoundDeclaration found)
        {
            if (found.GetIsEmpty()) return false;
            MemberModel member = found.member;
            int pos = Sci.CurrentPos;
            int line = Sci.LineFromPosition(pos);
            if (member != null)
            {
                while (line <= member.LineTo)
                {
                    string text = Sci.GetLine(line);
                    if (!string.IsNullOrEmpty(text))
                    {
                        Match m = Regex.Match(text, methodPattern);
                        if (m.Success)
                        {
                            string mText = m.Groups[0].Value;
                            int start = Sci.PositionFromLine(line);
                            int end = start + text.IndexOf(mText) + mText.Length;
                            if (end > pos) return true;
                            return false;
                        }
                    }
                    line++;
                }
                return false;
            }
            ClassModel aClass = found.inClass;
            while (line <= aClass.LineTo)
            {
                string text = Sci.GetLine(line);
                if (!string.IsNullOrEmpty(text))
                {
                    Match m = Regex.Match(text, classPattern);
                    if (m.Success)
                    {
                        string mText = m.Groups[0].Value;
                        int start = Sci.PositionFromLine(line);
                        int end = start + text.IndexOf(mText) + mText.Length;
                        if (end > pos) return true;
                        return false;
                    }
                }
                line++;
            }
            return false;
        }

        private static void ShowChangeClass(FoundDeclaration found)
        {
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            if((found.inClass.Flags & FlagType.Final) == 0)
            {
                string label = @"Make final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassFinal, null, found.inClass));
            }
            else
            {
                string label = @"Make not final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassNotFinal, null, found.inClass));
            }
            if((found.inClass.Flags & FlagType.Extern) == 0)
            {
                string label = @"Make extern";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassExtern, null, found.inClass));
            }
            else
            {
                string label = @"Make not extern";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassNotExtern, null, found.inClass));
            }
            CompletionList.Show(known, false);
        }

        private static void ShowChangeMethod(FoundDeclaration found)
        {
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            if ((found.member.Flags & FlagType.Final) == 0)
            {
                string label = @"Make final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeMethodFinal, found.member, null));
            }
            else
            {
                string label = @"Make not final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeMethodNotFinal, found.member, null));
            }
            if ((found.member.Flags & FlagType.Static) == 0)
            {
                string label = @"Add static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.AddStaticModifier, found.member, null));
            }
            CompletionList.Show(known, false);
        }

        private static void MakeMemberFinal(ScintillaNet.ScintillaControl Sci, MemberModel member, string memberPattern)
        {
            int line = member.LineFrom;
            while(line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                Match m = Regex.Match(text, memberPattern);
                if(m.Success)
                {
                    string mText = m.Groups[0].Value;
                    int start = Sci.PositionFromLine(line) + text.IndexOf(mText);
                    int end = start + mText.Length;
                    Sci.SetSel(start, end);
                    Sci.ReplaceSel(@"@:final " + mText.TrimStart());
                    return;
                }
                line++;
            }
        }

        private static void MakeMemberNotFinal(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            int line = member.LineFrom;
            while (line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                Match m = Regex.Match(text, @"@:final\s");
                if(m.Success)
                {
                    string mText = m.Groups[0].Value;
                    int start = Sci.PositionFromLine(line) + text.IndexOf(mText);
                    int end = start + mText.Length;
                    Sci.SetSel(start, end);
                    if (mText.Trim().Length == text.Trim().Length) Sci.LineDelete();
                    else Sci.ReplaceSel("");
                    return;
                }
                line++;
            }
        }

        private static void MakeClassExtern(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            int line = member.LineFrom;
            while (line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                Match m = Regex.Match(text, classPattern);
                if (m.Success)
                {
                    string mText = m.Groups[0].Value;
                    int start = Sci.PositionFromLine(line) + text.IndexOf(mText);
                    int end = start + mText.Length;
                    Sci.SetSel(start, end);
                    Sci.ReplaceSel(@"extern " + mText.TrimStart());
                    return;
                }
                line++;
            }
        }

        private static void MakeClassNotExtern(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            int line = member.LineFrom;
            while (line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                Match m = Regex.Match(text, @"extern\s");
                if (m.Success)
                {
                    string mText = m.Groups[0].Value;
                    int start = Sci.PositionFromLine(line) + text.IndexOf(mText);
                    int end = start + mText.Length;
                    Sci.SetSel(start, end);
                    Sci.ReplaceSel("");
                    return;
                }
                line++;
            }
        }

        private static void AddStaticModifier(ScintillaNet.ScintillaControl Sci, MemberModel member, string memberPattern)
        {
            int line = member.LineFrom;
            while (line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                Match m = Regex.Match(text, memberPattern);
                if (m.Success)
                {
                    string mText = m.Groups[0].Value;
                    int start = Sci.PositionFromLine(line) + text.IndexOf(mText);
                    int end = start + mText.Length;
                    Sci.SetSel(start, end);
                    Sci.ReplaceSel(@"static " + mText.TrimStart());
                    return;
                }
                line++;
            }
        }
    }

    class FoundDeclaration
    {
        public MemberModel member = null;
        public ClassModel inClass = ClassModel.VoidClass;

        public FoundDeclaration()
        {
        }

        public bool GetIsEmpty()
        {
            return member == null && inClass == ClassModel.VoidClass;
        }
    }

    /// <summary>
    /// Available generators
    /// </summary>
    public enum GeneratorJobType : int
    {
        MakeClassFinal,
        MakeClassNotFinal,
        MakeClassExtern,
        MakeClassNotExtern,
        MakeMethodFinal,
        MakeMethodNotFinal,
        AddStaticModifier,
    }

    /// <summary>
    /// Generation completion list item
    /// </summary>
    class GeneratorItem : ICompletionListItem
    {
        private string label;
        private GeneratorJobType job;
        private MemberModel member;
        private ClassModel inClass;
        private object data;

        public GeneratorItem(string label, GeneratorJobType job, MemberModel member, ClassModel inClass)
        {
            this.label = label;
            this.job = job;
            this.member = member;
            this.inClass = inClass;
        }

        public GeneratorItem(string label, GeneratorJobType job, MemberModel member, ClassModel inClass, object data)
            : this(label, job, member, inClass)
        {

            this.data = data;
        }

        public string Label
        {
            get { return label; }
        }

        public string Description
        {
            get { return TextHelper.GetString("Info.GeneratorTemplate"); }
        }

        public System.Drawing.Bitmap Icon
        {
            get { return (System.Drawing.Bitmap)ASContext.Panel.GetIcon(PluginUI.ICON_DECLARATION); }
        }

        public string Value
        {
            get
            {
                PluginMain.GenerateJob(job, member, inClass, label, data);
                return null;
            }
        }

        public object Data
        {
            get { return data; }
        }
    }
}