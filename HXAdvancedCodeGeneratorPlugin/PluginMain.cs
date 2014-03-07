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
                case GeneratorJobType.MakeFinal:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeMethodFinal(Sci, member);
                    }
                    finally
                    {
                        Sci.EndUndoAction();
                    }
                    break;
                case GeneratorJobType.MakeNotFinal:
                    Sci.BeginUndoAction();
                    try
                    {
                        MakeMethodNotFinal(Sci, member);
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
            if(found.GetIsEmpty()) return false;
            if(found.member == null || (found.member.Flags & (FlagType.Static | FlagType.Constructor)) > 0) return false;
            if((found.member.Flags & FlagType.Function) > 0)
            {
                ShowChangeMethodFinalAccess(found);
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
                    while (line <= member.LineTo)
                    {
                        Match m = Regex.Match(Sci.GetLine(line), methodPattern);
                        if (m.Success)
                        {
                            string mText = m.Groups[0].Value;
                            string text = Sci.GetLine(line);
                            int start = Sci.PositionFromLine(line);
                            int end = start + text.IndexOf(mText) + mText.Length;
                            if (end > Sci.CurrentPos)
                            {
                                result.member = member;
                                return result;
                            }
                        }
                        line++;
                    }
                }
                    return result;
            }
            return result;
        }

        private static void ShowChangeMethodFinalAccess(FoundDeclaration found)
        {
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            if((found.member.Flags & FlagType.Final) == 0)
            {
                string label = "Make final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeFinal, found.member, found.inClass));
            }
            else
            {
                string label = "Make not final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeNotFinal, found.member, found.inClass));
            }
            CompletionList.Show(known, false);
        }

        private static void MakeMethodFinal(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            int line = member.LineFrom;
            while(line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                Match m = Regex.Match(text, methodPattern);
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

        private static void MakeMethodNotFinal(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            int line = member.LineFrom;
            while (line <= member.LineTo)
            {
                string text = Sci.GetLine(line);
                Match m = Regex.Match(text, @"@:final\s");
                if(m.Success)
                {
                    string mText = m.Groups[0].Value;
                    if (mText.Trim().Length == text.Trim().Length) Sci.LineDelete();
                    else
                    {
                        int start = Sci.PositionFromLine(line) + text.IndexOf(mText);
                        int end = start + mText.Length;
                        Sci.SetSel(start, end);
                        Sci.ReplaceSel("");
                    }
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
        MakeFinal,
        MakeNotFinal,
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