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

namespace AdvancedCodeGenerator
{
    public class PluginMain : IPlugin
    {
        private static Regex reModifiers = new Regex("^\\s*(\\$\\(Boundary\\))?([\\w ]+)(function|const|var)", RegexOptions.Compiled);
        private static Regex reModifier = new Regex("(public |protected |private |internal )", RegexOptions.Compiled);
        private static Regex reMember = new Regex("(class |const |var |function )", RegexOptions.Compiled);
        private static Dictionary<Visibility, string> vis2string = new Dictionary<Visibility, string>();
        private static Dictionary<Visibility, GeneratorJobType> vis2job = new Dictionary<Visibility, GeneratorJobType>();
        private static Settings settingObject;

        private string pluginName = "AdvancedCodeGenerator";
        private string pluginGuid = "92f41ee5-6d96-4f03-95a5-b46610fe5c2e";
        private string pluginHelp = "www.flashdevelop.org/community/";
        private string pluginDesc = "Haxe advanced code generator for the ASCompletion engine.";
        private string pluginAuth = "FlashDevelop Team";
        private string settingFilename;

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
            InitVis2String();
            InitVis2Job();
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
            string dataPath = Path.Combine(PathHelper.DataDir, "AdvancedCodeGenerator");
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

        private static void InitVis2String()
        {
            vis2string.Add(Visibility.Internal, "internal");
            vis2string.Add(Visibility.Private, "private");
            vis2string.Add(Visibility.Protected, "protected");
            vis2string.Add(Visibility.Public, "public");
        }

        private static void InitVis2Job()
        {
            vis2job.Add(Visibility.Internal, GeneratorJobType.MakeInternal);
            vis2job.Add(Visibility.Private, GeneratorJobType.MakePrivate);
            vis2job.Add(Visibility.Protected, GeneratorJobType.MakeProtected);
            vis2job.Add(Visibility.Public, GeneratorJobType.MakePublic);
        }

        public static void GenerateJob(GeneratorJobType job, MemberModel member, ClassModel inClass, string itemLabel, object data)
        {
            bool startWithModifiers = ASContext.CommonSettings.StartWithModifiers;
            bool isHaxe = GetLangIsHaxe();
            ContextFeatures features = ASContext.Context.Features;
            string finalKey = GetFinalKey();
            string staticKey = features.staticKey;
            string inlineKey = GetInlineKey();
            string noCompletionKey = GetNoCompletionKey();
            ScintillaNet.ScintillaControl Sci = ASContext.CurSciControl;
            Sci.BeginUndoAction();
            try
            {
                switch (job)
                {
                    case GeneratorJobType.MakeInternal:
                    case GeneratorJobType.MakePrivate:
                    case GeneratorJobType.MakeProtected:
                    case GeneratorJobType.MakePublic:
                        foreach(Visibility vis in vis2job.Keys)
                        {
                            if(vis2job[vis] == job)
                            {
                                member = inClass ?? member;
                                ChangeAccess(Sci, member, vis);
                                FixFinalModifierLocation(Sci, member);
                                if (startWithModifiers) FixModifiersLocation(Sci, member);
                                if (isHaxe)
                                {
                                    FixInlineModifierLocation(Sci, member);
                                    FixNoCompletionMetaLocation(Sci, member);
                                }
                                break;
                            }
                        }
                        break;
                    case GeneratorJobType.MakeClassFinal:
                    case GeneratorJobType.MakeMethodFinal:
                        member = inClass ?? member;
                        AddModifier(Sci, member, finalKey);
                        FixFinalModifierLocation(Sci, member);
                        break;
                    case GeneratorJobType.MakeClassNotFinal:
                    case GeneratorJobType.MakeMethodNotFinal:
                        RemoveModifier(Sci, inClass ?? member, finalKey);
                        break;
                    case GeneratorJobType.MakeClassExtern:
                        AddModifier(Sci, inClass, features.intrinsicKey);
                        break;
                    case GeneratorJobType.MakeClassNotExtern:
                        RemoveModifier(Sci, inClass, features.intrinsicKey);
                        break;
                    case GeneratorJobType.AddStaticModifier:
                        if (!string.IsNullOrEmpty(finalKey)) RemoveModifier(Sci, member, finalKey);
                        AddModifier(Sci, member, staticKey);
                        FixFinalModifierLocation(Sci, member);
                        if (startWithModifiers) FixModifiersLocation(Sci, member);
                        if (isHaxe)
                        {
                            FixInlineModifierLocation(Sci, member);
                            FixNoCompletionMetaLocation(Sci, member);
                        }
                        break;
                    case GeneratorJobType.RemoveStaticModifier:
                        RemoveModifier(Sci, member, staticKey);
                        break;
                    case GeneratorJobType.AddInlineModifier:
                        AddModifier(Sci, member, inlineKey);
                        FixInlineModifierLocation(Sci, member);
                        break;
                    case GeneratorJobType.RemoveInlineModifier:
                        RemoveModifier(Sci, member, inlineKey);
                        break;
                    case GeneratorJobType.AddNoCompletionMeta:
                        AddModifier(Sci, member, noCompletionKey);
                        FixNoCompletionMetaLocation(Sci, member);
                        break;
                    case GeneratorJobType.RemoveNoCompletionMeta:
                        RemoveModifier(Sci, member, noCompletionKey);
                        break;
                }
            }
            finally
            {
                Sci.EndUndoAction();
            }
        }

        private static bool ContextualGenerator(ScintillaNet.ScintillaControl Sci)
        {
            FoundDeclaration found = GetDeclarationAtLine(Sci, Sci.LineFromPosition(Sci.CurrentPos));
            if (!GetDeclarationIsValid(Sci, found)) return false;
            if (found.member == null && found.inClass != ClassModel.VoidClass)
            {
                ShowChangeClass(found);
                return true;
            }
            FlagType flags = found.member.Flags;
            if ((flags & FlagType.Constructor) > 0)
            {
                ShowChangeConstructor(found);
                return true;
            }
            if ((flags & FlagType.Function) > 0)
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

        private static bool GetLangIsHaxe()
        {
            IProject project = PluginBase.CurrentProject;
            return project != null && project.Language.StartsWith("haxe");
        }

        private static string GetFinalKey()
        {
            IProject project = PluginBase.CurrentProject;
            if (project == null) return string.Empty;
            if (project.Language.StartsWith("haxe")) return "@:final";
            return ASContext.Context.Features.finalKey;
        }

        private static string GetInlineKey()
        {
            return GetLangIsHaxe() ? "inline" : string.Empty;
        }

        private static string GetNoCompletionKey()
        {
            return GetLangIsHaxe() ? "@:noCompletion" : string.Empty;
        }

        private static bool GetDeclarationIsValid(ScintillaNet.ScintillaControl Sci, FoundDeclaration found)
        {
            return found.GetIsEmpty() ? false : GetCurrentPosIsValid(Sci, found.member ?? found.inClass);
        }

        private static bool GetCurrentPosIsValid(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            for (int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (string.IsNullOrEmpty(text)) continue;
                Match m = reMember.Match(text);
                if (!m.Success) continue;
                int curPos = Sci.CurrentPos;
                int start = Sci.PositionFromLine(line);
                //TODO: remove comments
                //TODO: get real length
                return (start + m.Index + m.Length) > curPos || (start + text.TrimEnd().Length) == curPos;
            }
            return false;
        }

        private static void ShowChangeClass(FoundDeclaration found)
        {
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            Visibility classModifiers = ASContext.Context.Features.classModifiers;
            bool isHaxe = GetLangIsHaxe();
            ClassModel inClass = found.inClass;
            FlagType flags = inClass.Flags;
            Visibility access = inClass.Access;
            bool isPrivate = (inClass.Access & Visibility.Private) > 0;
            bool hasFinal = !string.IsNullOrEmpty(GetFinalKey());
            bool isFinal = (flags & FlagType.Final) > 0;
            bool isExtern = (flags & FlagType.Extern) > 0;
            if(isHaxe)
            {
                foreach (Visibility vis in vis2string.Keys)
                {
                    if ((access & vis) == 0 && (classModifiers & vis) > 0)
                    {
                        string label = "Make " + vis2string[vis];//TODO: localize it
                        known.Add(new GeneratorItem(label, vis2job[vis], null, inClass));
                    }
                }
            }
            if (hasFinal && !isFinal)
            {
                string label = "Make final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassFinal, null, inClass));
            }
            if (isHaxe && !isExtern)
            {
                string label = "Make extern";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassExtern, null, inClass));
            }
            if (hasFinal && isFinal)
            {
                string label = "Make not final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassNotFinal, null, inClass));
            }
            if (isHaxe && isExtern)
            {
                string label = "Make not extern";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeClassNotExtern, null, inClass));
            }
            CompletionList.Show(known, false);
        }

        private static void ShowChangeConstructor(FoundDeclaration found)
        {
            if (!GetLangIsHaxe()) return;
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            Visibility classModifiers = ASContext.Context.Features.classModifiers;
            MemberModel member = found.member;
            Visibility access = member.Access;
            foreach (Visibility vis in vis2string.Keys)
            {
                if ((access & vis) == 0 && (classModifiers & vis) > 0)
                {
                    string label = "Make " + vis2string[vis];//TODO: localize it
                    known.Add(new GeneratorItem(label, vis2job[vis], member, null));
                }
            }
            CompletionList.Show(known, false);
        }

        private static void ShowChangeMethod(FoundDeclaration found)
        {
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            Visibility methodModifiers = ASContext.Context.Features.methodModifiers;
            MemberModel member = found.member;
            FlagType flags = member.Flags;
            Visibility access = member.Access;
            bool isPrivate = (member.Access & Visibility.Private) > 0;
            bool hasStatics = ASContext.Context.Features.hasStatics;
            bool isStatic = (flags & FlagType.Static) > 0;
            bool hasFinal = !string.IsNullOrEmpty(GetFinalKey());
            bool isFinal = (flags & FlagType.Final) > 0;
            ScintillaNet.ScintillaControl Sci = ASContext.CurSciControl;
            string inlineKey = GetInlineKey();
            bool hasInline = !string.IsNullOrEmpty(inlineKey);
            bool isInline = hasInline ? GetHasModifier(Sci, member, inlineKey + "\\s") : false;
            string noCompletionKey = GetNoCompletionKey();
            bool hasNoCompletion = !string.IsNullOrEmpty(noCompletionKey);
            bool isNoCompletion = hasNoCompletion ? GetHasModifier(Sci, member, noCompletionKey + "\\s") : false;
            foreach (Visibility vis in vis2string.Keys)
            {
                if ((access & vis) == 0 && (methodModifiers & vis) > 0)
                {
                    string label = "Make " + vis2string[vis];//TODO: localize it
                    known.Add(new GeneratorItem(label, vis2job[vis], member, null));
                }
            }
            if (hasFinal && !isStatic && !isFinal)
            { 
                string label = "Make final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeMethodFinal, member, null));
            }
            if (hasStatics && !isStatic)
            {
                string label = "Add static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.AddStaticModifier, member, null));
            }
            if (hasInline && !isInline)
            {
                string label = "Add inline modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.AddInlineModifier, member, null));
            }
            if (hasNoCompletion && !isNoCompletion)
            {
                string label = "Add @:noCompletion";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.AddNoCompletionMeta, member, null));
            }
            if (hasFinal && !isStatic && isFinal)
            {
                string label = "Make not final";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.MakeMethodNotFinal, member, null));
            }
            if (hasStatics && isStatic)
            {
                string label = "Remove static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.RemoveStaticModifier, member, null));
            }
            if (hasInline && isInline)
            {
                string label = "Remove inline modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.RemoveInlineModifier, member, null));
            }
            if (hasNoCompletion && isNoCompletion)
            {
                string label = "Remove @:noCompletion";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.RemoveNoCompletionMeta, member, null));
            }
            CompletionList.Show(known, false);
        }

        private static void ShowChangeVariable(FoundDeclaration found)
        {
            List<ICompletionListItem> known = new List<ICompletionListItem>();
            Visibility varModifiers = ASContext.Context.Features.varModifiers;
            MemberModel member = found.member;
            FlagType flags = member.Flags;
            Visibility access = member.Access;
            ScintillaNet.ScintillaControl Sci = ASContext.CurSciControl;
            bool isPrivate = (member.Access & Visibility.Private) > 0;
            bool hasStatics = ASContext.Context.Features.hasStatics;
            bool isStatic = (flags & FlagType.Static) > 0;
            string noCompletionKey = GetNoCompletionKey();
            bool hasNoCompletion = !string.IsNullOrEmpty(noCompletionKey);
            bool isNoCompletion = hasNoCompletion ? GetHasModifier(Sci, member, noCompletionKey + "\\s") : false;
            foreach (Visibility vis in vis2string.Keys)
            {
                if ((access & vis) == 0 && (varModifiers & vis) > 0)
                {
                    string label = "Make " + vis2string[vis];//TODO: localize it
                    known.Add(new GeneratorItem(label, vis2job[vis], member, null));
                }
            }
            if (hasStatics && !isStatic)
            {
                string label = "Add static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.AddStaticModifier, member, null));
            }
            if (hasNoCompletion && !isNoCompletion)
            {
                string label = "Add @:noCompletion";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.AddNoCompletionMeta, member, null));
            }
            if (hasStatics && isStatic)
            {
                string label = "Remove static modifier";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.RemoveStaticModifier, member, null));
            }
            if (hasNoCompletion && isNoCompletion)
            {
                string label = "Remove @:noCompletion";//TODO: localize it
                known.Add(new GeneratorItem(label, GeneratorJobType.RemoveNoCompletionMeta, member, null));
            }
            CompletionList.Show(known, false);
        }

        private static bool GetHasModifier(ScintillaNet.ScintillaControl Sci, MemberModel member, string modifier)
        {
            for (int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (!string.IsNullOrEmpty(text) && Regex.IsMatch(text, modifier)) return true;
            }
            return false;
        }

        private static void ChangeAccess(ScintillaNet.ScintillaControl Sci, MemberModel member, Visibility vis)
        {
            for (int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (string.IsNullOrEmpty(text)) continue;
                Match m = reMember.Match(text);
                if (!m.Success) continue;
                int start = Sci.PositionFromLine(line);
                Sci.SetSel(start, start + text.Length);
                string access = vis2string[vis] + " ";
                if (GetLangIsHaxe() && (((member.Flags & FlagType.Class) > 0 && (vis & Visibility.Public) > 0) || ((vis & Visibility.Private) > 0 && !settingObject.UsePrivateExplicitly))) access = "";
                m = reModifier.Match(text);
                if (m.Success) text = text.Remove(m.Index, m.Length).Insert(m.Index, access);
                else
                {
                    m = Regex.Match(text, "[@:\\w ]", RegexOptions.IgnoreCase);
                    text = text.Insert(m.Index, access);
                }
                Sci.ReplaceSel(text);
                return;
            }
        }

        private static void AddModifier(ScintillaNet.ScintillaControl Sci, MemberModel member, string modifier)
        {
            for (int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (string.IsNullOrEmpty(text)) continue;
                Match m = reMember.Match(text);
                if (!m.Success) continue;
                int start = Sci.PositionFromLine(line) + m.Index;
                Sci.SetSel(start, start + m.Length);
                Sci.ReplaceSel(modifier + " " + m.Value);
                return;
            }
        }

        private static void RemoveModifier(ScintillaNet.ScintillaControl Sci, MemberModel member, string modifier)
        {
            for(int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (string.IsNullOrEmpty(text)) continue;
                Match m = Regex.Match(text, modifier + "\\s");
                if (!m.Success) continue;
                int start = Sci.PositionFromLine(line) + m.Index;
                Sci.SetSel(start, start + m.Length);
                Sci.ReplaceSel("");
                return;
            }
        }

        private static void FixModifiersLocation(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            for (int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (string.IsNullOrEmpty(text) || !reMember.IsMatch(text)) continue;
                Match m1 = reModifiers.Match(text);
                if (!m1.Success) continue;
                Group decl = m1.Groups[2];
                Match m2 = reModifier.Match(decl.Value);
                if (!m2.Success) continue;
                int start = Sci.PositionFromLine(line) + decl.Index;
                Sci.SetSel(start, start + decl.Length);
                Sci.ReplaceSel(m2.Value + decl.Value.Remove(m2.Index, m2.Length));
                return;
            }
        }

        private static void FixFinalModifierLocation(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            for (int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (string.IsNullOrEmpty(text)) continue;
                string key = GetFinalKey();
                Match m = Regex.Match(text, key + "\\s");
                if (!m.Success) continue;
                if (m.Index == 0) return;
                Group decl = m.Groups[0];
                m = Regex.Match(text, "[@:\\w ]", RegexOptions.IgnoreCase);
                int insertStart = m.Success ? m.Index : 0;
                int start = Sci.PositionFromLine(line);
                Sci.SetSel(start, start + text.Length);
                Sci.ReplaceSel(text.Remove(decl.Index, decl.Length).Insert(insertStart, key + " "));
                return;
            }
        }

        private static void FixInlineModifierLocation(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            for (int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (string.IsNullOrEmpty(text)) continue;
                string key = GetInlineKey();
                Match m = Regex.Match(text, key + "\\s");
                if (!m.Success) continue;
                int start = Sci.PositionFromLine(line);
                Sci.SetSel(start, start + text.Length);
                text = text.Remove(m.Index, m.Length);
                m = reMember.Match(text);
                Sci.ReplaceSel(text.Insert(m.Index, key + " "));
                return;
            }
        }

        private static void FixNoCompletionMetaLocation(ScintillaNet.ScintillaControl Sci, MemberModel member)
        {
            for (int line = member.LineFrom; line <= member.LineTo; line++)
            {
                string text = Sci.GetLine(line);
                if (string.IsNullOrEmpty(text)) continue;
                string key = GetNoCompletionKey();
                Match m = Regex.Match(text, key + "\\s");
                if (!m.Success) continue;
                if (m.Index == 0) return;
                Group decl = m.Groups[0];
                m = Regex.Match(text, "[@:\\w ]", RegexOptions.IgnoreCase);
                int insertStart = m.Success ? m.Index : 0;
                int start = Sci.PositionFromLine(line);
                Sci.SetSel(start, start + text.Length);
                Sci.ReplaceSel(text.Remove(decl.Index, decl.Length).Insert(insertStart, key + " "));
                return;
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
        RemoveInlineModifier,
        AddNoCompletionMeta,
        RemoveNoCompletionMeta,
        MakeInternal,
        MakePrivate,
        MakeProtected,
        MakePublic,
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