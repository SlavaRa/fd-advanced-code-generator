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

        private static Regex reModifiers = new Regex("^\\s*(\\$\\(Boundary\\))?([a-z ]+)(function|var)", RegexOptions.Compiled);
        private static Regex reModifier = new Regex("(public |private )", RegexOptions.Compiled);
        private static Regex reMember = new Regex("(class |var |function )", RegexOptions.Compiled);
        private const string methodPattern = "function\\s+[a-z_0-9.]+\\s*\\(";
        private const string classPattern = "class\\s+[a-z_0-9.]+\\s*";
        private const string varPattern = "var\\s+[a-z_0-9.]+\\s*";

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
            if (!GetLangIsValid()) return;
            ScintillaNet.ScintillaControl Sci = ASContext.CurSciControl;
            switch (job)
            {
                case GeneratorJobType.MakeClassFinal:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeMemberFinal(Sci, inClass);
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
                        MakeMemberFinal(Sci, member);
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
                        RemoveModifier(Sci, inClass ?? member, "@:final\\s");
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
                        RemoveModifier(Sci, inClass, "extern\\s");
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
                        if ((member.Flags & FlagType.Function) > 0)
                        {
                            RemoveModifier(Sci, member, "@:final\\s");
                            AddStaticModifier(Sci, member);
                        }
                        else AddStaticModifier(Sci, member);
                    }
                    finally
                    {
                        Sci.EndUndoAction();
                    }
                    break;
                case GeneratorJobType.RemoveStaticModifier:
                    Sci.BeginUndoAction();
                    try
                    {
                        RemoveModifier(Sci, member, "static\\s");
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
            FlagType flags = found.member.Flags;
            if ((flags & FlagType.Constructor) == 0 && (flags & FlagType.Function) > 0)
            {
                ShowChangeMethod(found);
                return true;
            }
            if ((flags & FlagType.LocalVar) == 0 && (flags & (FlagType.Variable | FlagType.Getter | FlagType.Setter)) > 0)
            {
                ShowChangeVariable(found);
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
            var line = -1;
            if (member != null)
            {
                line = member.LineFrom;
                while (line <= member.LineTo)
                {
                    string text = Sci.GetLine(line);
                    if (!string.IsNullOrEmpty(text))
                    {
                        Match m1 = Regex.Match(text, methodPattern, RegexOptions.IgnoreCase);
                        Match m2 = Regex.Match(text, varPattern, RegexOptions.IgnoreCase);
                        string mText = string.Empty;
                        if (m1.Success) mText = m1.Groups[0].Value;
                        else if (m2.Success) mText = m2.Groups[0].Value;
                        else continue;
                        int start = Sci.PositionFromLine(line);
                        int end = start + text.IndexOf(mText) + mText.Length;
                        if (end > pos) return true;
                        return false;
                    }
                    line++;
                }
                return false;
            }
            ClassModel aClass = found.inClass;
            line = aClass.LineFrom;
            while (line <= aClass.LineTo)
            {
                string text = Sci.GetLine(line);
                if (!string.IsNullOrEmpty(text))
                {
                    Match m = Regex.Match(text, classPattern, RegexOptions.IgnoreCase);
                    if (!m.Success) continue;
                    string mText = m.Groups[0].Value;
                    int start = Sci.PositionFromLine(line);
                    int end = start + text.IndexOf(mText) + mText.Length;
                    if (end > pos) return true;
                    return false;
                }
                line++;
            }
            return false;
        }

        private static bool GetLangIsValid()
        {
            IProject project = PluginBase.CurrentProject;
            if (project == null) return false;
            return project.Language.StartsWith("haxe");
        }

        private static void ShowChangeClass(FoundDeclaration found)
        {
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            FlagType flags = found.inClass.Flags;
            bool isFinal = (flags & FlagType.Final) > 0;
            bool isExtern = (flags & FlagType.Extern) > 0;
            if (!isFinal)
            {
                string label = @"Make final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassFinal, null, found.inClass));
            }
            if (!isExtern)
            {
                string label = @"Make extern";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassExtern, null, found.inClass));
            }
            if (isFinal)
            {
                string label = @"Make not final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassNotFinal, null, found.inClass));
            }
            if (isExtern)
            {
                string label = @"Make not extern";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassNotExtern, null, found.inClass));
            }
            CompletionList.Show(known, false);
        }

        private static void ShowChangeMethod(FoundDeclaration found)
        {
            FlagType flags = found.member.Flags;
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            bool isStatic = (flags & FlagType.Static) > 0;
            bool isFinal = (flags & FlagType.Final) > 0;
            if (!isStatic && !isFinal)
            { 
                string label = @"Make final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeMethodFinal, found.member, null));
            }
            if (!isStatic)
            {
                string label = @"Add static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.AddStaticModifier, found.member, null));
            }
            if (!isStatic && isFinal)
            {
                string label = @"Make not final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeMethodNotFinal, found.member, null));
            }
            if (isStatic)
            {
                string label = @"Remove static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.RemoveStaticModifier, found.member, null));
            }
            CompletionList.Show(known, false);
        }

        private static void ShowChangeVariable(FoundDeclaration found)
        {
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            FlagType flags = found.member.Flags;
            bool isStatic = (flags & FlagType.Static) > 0;
            if (!isStatic)
            {
                string label = @"Add static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.AddStaticModifier, found.member, null));
            }
            if (isStatic)
            {
                string label = @"Remove static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.RemoveStaticModifier, found.member, null));
            }
            CompletionList.Show(known, false);
        }

        private static void MakeMemberFinal(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            int line = member.LineFrom;
            while(line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                if(!string.IsNullOrEmpty(text))
                {
                    Match m = reMember.Match(text);
                    if (m.Success)
                    {
                        m = Regex.Match(text, @"[a-z_0-9].", RegexOptions.IgnoreCase);
                        string mValue = m.Groups[0].Value;
                        int start = Sci.PositionFromLine(line) + text.IndexOf(mValue);
                        int end = start + mValue.Length;
                        Sci.SetSel(start, end);
                        Sci.ReplaceSel(@"@:final " + mValue.TrimStart());
                    }
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
                if (!string.IsNullOrEmpty(text))
                {
                    Match m = reMember.Match(text);
                    if (m.Success)
                    {
                        //m = Regex.Match(text, "[a-z_0-9].", RegexOptions.IgnoreCase);
                        string mValue = m.Groups[0].Value;
                        int start = Sci.PositionFromLine(line) + text.IndexOf(mValue);
                        int end = start + mValue.Length;
                        Sci.SetSel(start, end);
                        Sci.ReplaceSel("extern " + mValue.TrimStart());
                    }
                    return;
                }
                line++;
            }
        }

        private static void AddStaticModifier(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            int line = member.LineFrom;
            while (line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                if (!string.IsNullOrEmpty(text))
                {
                    Match m = reMember.Match(text);
                    if (m.Success)
                    {
                        m = Regex.Match(text, "[a-z_0-9].", RegexOptions.IgnoreCase);
                        string mValue = m.Groups[0].Value;
                        int start = Sci.PositionFromLine(line) + text.IndexOf(mValue);
                        Sci.SetSel(start, start + mValue.Length);
                        Sci.ReplaceSel("static " + mValue.TrimStart());
                        if (ASContext.CommonSettings.StartWithModifiers) FixModifiersLocation(Sci, line);
                        return;
                    }
                }
                line++;
            }
        }

        private static void RemoveModifier(ScintillaNet.ScintillaControl Sci, MemberModel member, string modifier)
        {
            int line = member.LineFrom;
            while (line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                if (!string.IsNullOrEmpty(text))
                {
                    Match m = Regex.Match(text, modifier);
                    if (m.Success)
                    {
                        string mValue = m.Groups[0].Value;
                        int start = Sci.PositionFromLine(line) + text.IndexOf(mValue);
                        Sci.SetSel(start, start + mValue.Length);
                        Sci.ReplaceSel("");
                        return;
                    }
                }
                line++;
            }
        }

        private static void FixModifiersLocation(ScintillaNet.ScintillaControl Sci, int line)
        {
            string text = Sci.GetLine(line);
            Match m = reModifiers.Match(text);
            if (m.Success)
            {
                Group decl = m.Groups[2];
                Match m2 = reModifier.Match(decl.Value);
                if (m2.Success)
                {
                    int start = Sci.PositionFromLine(line);
                    Sci.SetSel(start + decl.Index, start + decl.Length);
                    Sci.ReplaceSel((m2.Value + decl.Value.Remove(m2.Index, m2.Length)).TrimEnd());
                }
            }
        }
    }

    class FoundDeclaration
    {
        public MemberModel member;
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
        RemoveStaticModifier,
        AddInlineModifier,
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