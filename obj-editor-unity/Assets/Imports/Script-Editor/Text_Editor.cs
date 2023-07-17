//All c# keywords by type
//https://www.w3schools.in/csharp-tutorial/keywords/
//https://www.w3schools.in/csharp-tutorial/operators-in-c/

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Rect_Extension;
using UnityEngine.EventSystems;

public class Text_Editor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

string note = @"
Works on windows only - I can't figure how to get CapsLock state without winAPI :(

Navigation
---------
Left/Right/Up/Down arrows       - move cursor
PgUp/PgDown                     - scroll one page up/down
Home/End                        - move cursor to start/end of the row
Ctrl + Home/End                 - to the start/end of the text
Ctrl + Left/Right               - to the next/previous word
Ctrl + Up/Down                  - scroll one row up/down
Mouse wheel                     - vertical scroll
Alt + Mouse wheel               - horizontal scroll
Ctrl + Mouse wheel              - change font size (optional)

Editing
---------
Correctly handle tab / shift+tab to incriment/decrement identation with single or multiple selected rows
Clipboard support - cut/copy/paste (ctrl + c, ctrl + v, ctrl + x)
Undo support - ctrl + z (experimental)
Configurable key repeat timer
Insert / overtype modes (use insert key to toggle)
Autoclose ( { [ ' ""

Selection
---------
Ctrl + A            - select all
Shift + arrows      - selecct next/previous row/character
Mouse selection     - shift + click or drag
Double click        - select word
Triple click        - select full row

Code highlight
--------------
Highlight for text in quotes, comments (single-line only), and some hardcoded c# keywords (modifiable in code)

Intellisense
------------
Get all NET classes from reflection - init is done in background to not freeze the main thread, 2.1sec to get ~85k of net types
Variable detection is also working in the background
Option to exclude some namespaces for parsing
Detect variables by template: some_class var_name;
Detect variables by template: var var_name = new some_class();
Detect literals i.e. var f = 3f; var s = ""asdas"";
Detect variable context
Handle array/collections, including multidimensional arrays, array of array, array of collection e.t.c... (experemental)
Correctly suggest instance methods for variables and static methods for typed chains (i.e. Vector3.)
Handle ""using"" keyword
Handle indexer property i.e. enumerables
Handle generic classes i.e. Dictionary<string, int>
Very basic handling of inheritance / interfaces (memory consuming, limited to System.Collections namespace for now)
Handle extension methods i.e. linq (experimental)

Intellisense - What does not work yet
-------------------------------------
Only one variable with the same name is handled, even in different contexts
Overloaded methods
Static/Instance members of structures not detected i.e. var x = 5f; x.--- will incorrectly suggest MinValue/MaxValue, because float is a struct
Methods with 2 or more generic arguments <T1, T2> works incorrectly
Classes/methods does not updated in realtime, so if your write new new method - it won't appear in code suggestion

";

    //Public parameters
    public Font font;
    public int font_size = 14;
    public int tab_spaces = 3;
    public float cursor_width = 3f;
    public float cursor_blink_rate = 0.4f;

    float cursor_blink_timer = 0f;

    //Focus
    public Activate_Scheme_Info Activate_Settings = new Activate_Scheme_Info();
    bool mouse_is_over_me = false;
    bool mouse_is_over_my_child = false;
    [System.Serializable]
    public class Activate_Scheme_Info {
        public bool IsActive = true;
        public Activate_Enum Activate = Activate_Enum.OnClick;
        public DeActivate_Enum DeActivate = DeActivate_Enum.OnClickOutside;
        public bool Hide_Cursor_When_Inactive = true;
        public GameObject Activation_Border = null;
        public enum Activate_Enum { OnClick, OnMouseOver, Never };
        public enum DeActivate_Enum { OnClickOutside, OnMouseOut, Never };
    }

    //Modes
    bool insert_mode = false;
    enum nav_enum { Left, Right, Up, Down, PgUp, PgDn, Home, End, Ctrl_Home, Ctrl_End, Word_Left, Word_Right, Row_Up, Row_Down, Mouse_Scroll, Mouse_ScrollH, Submit }

    //References
    GameObject cursor;                  //Cursor
    GameObject text_template = null;    //Text object template
    Transform txt_cnt = null;           //Text container Transform
    RectTransform txt_cnt_rt = null;    //Text container RectTransform

    //Internals text tracking
    public int current_row = 0;
    public int current_chr = 0;
    int current_chr_previous = 0;
    float lineHeight = 0f;
    List<Text> text_rows = new List<Text>();             //Text components per row
    List<List<float>> text_char_w = new List<List<float>>(); //Char width per row > per char
    List<float> text_rows_w = new List<float>();             //Row width in pixel
    float canvas_scale = 1f;
    int scaled_font_size = 0;
    Vector3 old_scale = Vector2.zero;
    Vector2 old_size = Vector2.zero;

    //Navigation
    public bool allow_fontsize_change = true;
    public Vector2Int fontsize_change_limit = new Vector2Int(6, 100);

    //Key repeating
    public float repeat_start = 0.7f;
    public float repeat_ratio = 0.2f;
    float repeat_start_timer = -1f;
    float repeat_ratio_timer = -1f;
    KeyCode repeat_key = KeyCode.None;

    //Selection
    bool ready_to_select = false;
    int[] selection_start = new int[]{0,0};
    int[] selection_end   = new int[]{0,0};
    GameObject selector;                //Selector template
    Dictionary<int, RectTransform> selector_per_row = new Dictionary<int, RectTransform>(); //Selectors per row
    List<GameObject> selector_highlighters = new List<GameObject>();

    //Undo
    public int undo_limit = 50;
    List< List<Undo_struct> > Undo_Buffer = new List< List<Undo_struct> >();
    float undo_last_set_time = -9999f;
    struct Undo_struct {
        public undo_op operation;
        public string txt;
        public int line_ind;
        public Vector2Int cursor_pos;
    }
    enum undo_op { modify, add, remove };

    //DblClick
    float dbl_click_max_delay = 0.25f;
    float dbl_click_max_offset = 2f;
    click_info_struct click_info = new click_info_struct();
    struct click_info_struct {
        public float last_time;
        public Vector2 last_pos;
        public bool dbl_clicked;
        //public int click_count;
    }

    //Scroll bars
    float cur_scroll_H = 0f;
    float cur_scroll_V = 0f;
    public Scrollbar ScrollH = null;    //Horizontal scroll-bar
    public Scrollbar ScrollV = null;    //Vertical scroll-bar

    //Typing keys
    public string Auto_Close_Pairs = "[];();{};'';\"\"";
    Dictionary<string, string> Auto_Close_Pairs_Dict = new Dictionary<string, string>();
    Dictionary<KeyCode, char> keyinfo = new Dictionary<KeyCode, char>();    //All typing keys codes and characters
    Dictionary<KeyCode, char> keyUpper = new Dictionary<KeyCode, char>();   //Uppercase char for keys

    //Colors
    public Color_Scheme_Info Color_Scheme = new Color_Scheme_Info();
    [System.Serializable]
    public class Color_Scheme_Info {
        public Color background = new Color32(30, 30, 30, 255);
        public Color cursor = new Color32(255, 255, 255, 255);
        public Color scrollbar_back = new Color32(30, 30, 30, 255);
        public Color scrollbar_front = new Color32(66, 66, 66, 255);
        public Color scrollbar_front_highlight = new Color32(88, 88, 88, 255);
        public Color scrollbar_front_pressed = new Color32(109, 109, 109, 255);
        public Color selection_color = new Color32(58, 61, 65, 255);
        public Color highlight_default_color = new Color32(200, 32, 32, 180);
        public Color highlight_breakpoint_color = new Color32(32, 32, 200, 180);
        public Color t_std = new Color32(156, 220, 254, 255);
        public Color t_sym = Color.white;
        public Color t_quote = new Color32(255, 165, 0, 255);
        public Color t_comment = new Color32(255, 165, 0, 255);
        public Color t_keyword = new Color32(197, 134, 192, 255);
        public Color t_modif = new Color32(86, 156, 214, 255);
        public Color t_types = new Color32(78, 201, 176, 255);
        public Color t_const = new Color32(220, 220, 170, 255);
        public Color t_other = new Color32(209, 105, 105, 255);
        public Color t_refl  = new Color32(206, 145, 120, 255);
        public Color Inactive_Tint = Color.white;
    }

    //Syntax highlighting
    public bool Syntax_Highlight_Enable = true;
    string charset_alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz0123456789";
    string[] word_types = new string[]{ "g", "usemtl", "mtllib"};
    string[] word_const = new string[]{ "f", "v", "vn", "vt", "l", "p" };
    string[] word_modif = new string[]{ };
    string[] word_key   = new string[]{ };
    string[] word_refl  = new string[]{ };
    string[] word_other = new string[]{ };
    List<Text> colored_text = new List<Text>();

    //Code suggestion
    bool suggestion_mouse_enabled = true;
    public Suggestion_Info Intellisense_Settings = new Suggestion_Info();
    Vector2Int current_suggestion = new Vector2Int(-1, -1);
    List<string> current_suggestion_signatures = new List<string>();
    classes_dict intellisense_vars = new classes_dict();
    List<classes_dict> intellisense_usings = new List<classes_dict>();
    classes_dict classes_dictionary = new classes_dict();
    Dictionary<string, string> builtin_types = new Dictionary<string, string>();
    Regex paranthesis = new Regex(@"\(.*?\)", RegexOptions.Compiled);
    Regex brackets = new Regex(@"\[.*?\]", RegexOptions.Compiled);
    Regex brackets_content = new Regex(@"\[.+?\]", RegexOptions.Compiled);
    //Regex var_var = new Regex(@"var\s+(\w+)\s*=(\s*new)?\s*(([\w\.](\s*<.*>)?(\s*\(.*?\))?(\[\d*(,\d)*\]({.*})?)?)+)\s*;", RegexOptions.Compiled);
    Regex var_var = new Regex(@"var\s+(\w+)\s*=(\s*new)?\s*(([\w\.""](\s*<.*>)?(\s*\(.*?\))?((\[[\d|,]*\])*({.*})?)?)+)\s*;", RegexOptions.Compiled);
    Regex var_common = new Regex(@"\s*(public[\s]*)?(static[\s]*)?([\w\.]+(\s*<.+>)?(\s*\[,*\])*\s+[\w]+)\s*(=.+)?;", RegexOptions.Compiled);
    Regex using_regex = new Regex("using\\s+([\\w\\.]+);", RegexOptions.Compiled);
    BackgroundWorker check_var_thread = new BackgroundWorker();
    ConcurrentQueue<changed_rows_info> changed_rows = new ConcurrentQueue<changed_rows_info>();
    ConcurrentDictionary<string, variable_info[]> vars_rows_assoc = new ConcurrentDictionary<string, variable_info[]>();
    ConcurrentDictionary<Text, string[]> vars_rows_assoc_rev = new ConcurrentDictionary<Text, string[]>();
    ConcurrentDictionary<classes_dict, Text[]> using_rows_assoc = new ConcurrentDictionary<classes_dict, Text[]>();
    ConcurrentDictionary<Text, classes_dict[]> using_rows_assoc_rev = new ConcurrentDictionary<Text, classes_dict[]>();
    class classes_dict
	{
		public Dictionary<string, classes_dict> dict = new Dictionary<string, classes_dict>(StringComparer.OrdinalIgnoreCase);
        public Type_Enum type = Type_Enum.T_NotSet;
        public string full_name = "";
        public string return_type = "";
        public string return_type_full = "";
        public enum Type_Enum { N_Namespace, T_NotSet, T_class, T_Interface, T_struct, T_enum, T_Delegate, P_Property, P_Indexer, M_Method, M_ExtMethod, F_Field, F_CONST, F_READONLY, E_Event, Enum_Value };
        public string method_signature = "";
        public bool is_static = false;
        public List<string> derived_from = new List<string>();
        public List<classes_dict> derived_from_cl = new List<classes_dict>();
        public List<KeyValuePair<string, classes_dict>> T2 = new List<KeyValuePair<string, classes_dict>>();
        public classes_dict Make_Copy() {
            classes_dict new_cldt = new classes_dict();
            new_cldt.dict             = this.dict;
            new_cldt.type             = this.type;
            new_cldt.full_name        = this.full_name;
            new_cldt.return_type      = this.return_type;
            new_cldt.return_type_full = this.return_type_full;
            new_cldt.method_signature = this.method_signature;
            new_cldt.is_static        = this.is_static;
            new_cldt.derived_from     = this.derived_from;
            new_cldt.derived_from_cl  = this.derived_from_cl;
            new_cldt.T2               = this.T2.ToList();
            return new_cldt;
        }
	}
    class changed_rows_info {
        public changed_rows_info(Text r, string txt) { associated_row = r; text = txt; }
        public string text;
        public Text associated_row;
    }
    class variable_info {
        public variable_info(Text r, classes_dict d) { row = r; classes_dict = d; }
        public variable_info(Text r, classes_dict d, int[] c) { row = r; classes_dict = d; context = c; }
        public Text row;
        public classes_dict classes_dict;
        public int[] context = new int[]{};
    }
    [TextArea(6,10)] public string exclude_namespaces = "";
    [System.Serializable]
    public struct Suggestion_Info {
        public Sprite icon_not_set;
        public Sprite icon_namespace;
        public Sprite icon_class;
        public Sprite icon_interface;
        public Sprite icon_struct;
        public Sprite icon_enum;
        public Sprite icon_enum_value;
        public Sprite icon_method;
        public Sprite icon_method_extension;
        public Sprite icon_property;
        public Sprite icon_delegate;
        public Sprite icon_field;
        public Sprite icon_field_readonly;
        public Sprite icon_field_const;
        public Sprite icon_event;
        public RectTransform suggestion_rt; //Suggestion panel
        public GameObject suggestion_item;  //Suggestion item_template
        public Scrollbar suggestion_scroll;
        public Text method_signature_text;
        public bool get_assemblies_from_file;
        public bool get_assemblies_from_reflection;
        public bool write_assemblies_to_file;
        public bool write_namespaces_to_file;
        public int max_suggestion_count;
    }

    //Events
    public EventHandler OnTextChanged;
    public UnityEvent On_Intellisense_BeginInit = null;
    public UnityEvent On_Intellisense_EndInit = null;
    ConcurrentQueue<UnityEvent> Event_Queue = new ConcurrentQueue<UnityEvent>();

    //Code
    Dictionary<Text, GameObject> breakpoints = new Dictionary<Text, GameObject>();

    //TODO: This is windows only solution, and that's bad :(
    [System.Runtime.InteropServices.DllImport("USER32.dll")] public static extern short GetKeyState(int nVirtKey);

    // Start is called before the first frame update
    void Awake()
    {
        //Objects
        txt_cnt = transform.GetChild(0);
        txt_cnt_rt = txt_cnt.GetComponent<RectTransform>();

        //Fill typing codes and chars
        string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        foreach (char c in letters) keyinfo.Add( (KeyCode)System.Enum.Parse(typeof(KeyCode), c.ToString()), c.ToString().ToLower()[0] );
        string numbers = "1234567890";
        foreach (char c in numbers) {
            keyinfo.Add( (KeyCode)System.Enum.Parse(typeof(KeyCode), "Alpha" + c.ToString()), c );
            keyinfo.Add( (KeyCode)System.Enum.Parse(typeof(KeyCode), "Keypad" + c.ToString()), c );
        }
        keyinfo.Add(KeyCode.BackQuote, '`');
        keyinfo.Add(KeyCode.Minus, '-'); keyinfo.Add(KeyCode.Equals, '='); keyinfo.Add(KeyCode.Backslash, '\\');
        keyinfo.Add(KeyCode.LeftBracket, '['); keyinfo.Add(KeyCode.RightBracket, ']');
        keyinfo.Add(KeyCode.Semicolon, ';'); keyinfo.Add(KeyCode.Quote, '\'');
        keyinfo.Add(KeyCode.Comma, ','); keyinfo.Add(KeyCode.Period, '.'); keyinfo.Add(KeyCode.Slash, '/');
        keyinfo.Add(KeyCode.Space, ' ');
        keyinfo.Add(KeyCode.Tab, '\t');
        keyinfo.Add(KeyCode.KeypadDivide, '/'); keyinfo.Add(KeyCode.KeypadMultiply, '*');
        keyinfo.Add(KeyCode.KeypadMinus, '-'); keyinfo.Add(KeyCode.KeypadPlus, '+'); keyinfo.Add(KeyCode.KeypadPeriod, '.');

        string numbersU = ")!@#$%^&*(";
        for (int i = 0; i < 10; i++) keyUpper.Add( (KeyCode)System.Enum.Parse(typeof(KeyCode), "Alpha" + i.ToString()), numbersU[i] );
        keyUpper.Add(KeyCode.BackQuote, '~');
        keyUpper.Add(KeyCode.Minus, '_'); keyUpper.Add(KeyCode.Equals, '+'); keyUpper.Add(KeyCode.Backslash, '|');
        keyUpper.Add(KeyCode.LeftBracket, '{'); keyUpper.Add(KeyCode.RightBracket, '}');
        keyUpper.Add(KeyCode.Semicolon, ':'); keyUpper.Add(KeyCode.Quote, '"');
        keyUpper.Add(KeyCode.Comma, '<'); keyUpper.Add(KeyCode.Period, '>'); keyUpper.Add(KeyCode.Slash, '?');
        
        //Create cursor
        cursor = new GameObject("cursor");
        cursor.transform.SetParent(txt_cnt);
        RawImage img = cursor.AddComponent<RawImage>();
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.black); tex.Apply();
        img.texture = tex;
        RectTransform rt = cursor.GetComponent<RectTransform>();
        rt.localScale = new Vector3(1f, 1f, 1f);
        rt.localRotation = Quaternion.identity;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(cursor_width, 10f);
        rt.anchoredPosition3D = Vector3.zero;

        //Create selector
        GameObject selector_cnt = new GameObject("Selectors_container");
        selector_cnt.transform.SetParent(txt_cnt);
        selector_cnt.transform.SetSiblingIndex(0);
        selector_cnt.transform.localRotation = Quaternion.identity;
        rt = selector_cnt.AddComponent<RectTransform>();
        rt.anchoredPosition3D = Vector3.zero;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(0f, 0f);
        rt.localScale = new Vector3(1f, 1f, 1f);
        selector = Instantiate(cursor, selector_cnt.transform);
        selector.name = "Selector";
        Texture2D tex2 = new Texture2D(1, 1);
        tex2.SetPixel(0, 0, new Color32(128, 128, 255, 200) ); tex2.Apply();
        selector.GetComponent<RawImage>().texture = tex2;
        selector.SetActive(false);

        //Create text
        text_template = new GameObject("TextRow_Template");
        text_template.transform.SetParent(txt_cnt);
        
        Text t = text_template.AddComponent<Text>();
        t.font = font;
        t.fontSize = font_size;
        t.color = Color.black;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;

        rt = text_template.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(0f, 0f);
        rt.anchoredPosition3D = Vector3.zero;
        rt.localScale = new Vector3(1f, 1f, 1f);
        rt.localRotation = Quaternion.identity; //This is needed, if text_editor is used in world space canvas

        //Adjust cursor size to line height
        Canvas cnv = GetComponentInParent<Canvas>();
        var extents = t.cachedTextGenerator.rectExtents.size * 0.5f;
        lineHeight = t.cachedTextGeneratorForLayout.GetPreferredHeight("A", t.GetGenerationSettings(extents)) / cnv.scaleFactor;
        cursor.GetComponent<RectTransform>().sizeDelta = new Vector2(cursor_width, lineHeight);

        //Canvas scaling handling
        canvas_scale = cnv.transform.localScale.x;
        scaled_font_size = Mathf.FloorToInt(font_size * canvas_scale);

        text_template.SetActive(false);

        //Scrollbars events
        if (ScrollH != null) ScrollH.onValueChanged.AddListener((v)=> On_Scroll_H(v) );
        if (ScrollV != null) ScrollV.onValueChanged.AddListener((v)=> On_Scroll_V(v) );

        Color_Scheme_Apply();
        Create_New_Row();

        //To get character width call .font.GetCharacterInfo(chr, out ci, text_rows[current_row].fontSize);
        //But it will be set to 0 if canvas is not updated for this character
        //ForceUpdateCanvases is needed because a character needs to be rendered before we can get width
        //Here we render all possible characters and update the canvas once, instead of doing it each time the key is pressed
        //BUT - it's not working :(
        // string str = "";
        // foreach (var kv in keyinfo) str += kv.Value;
        // foreach (var kv in keyUpper) str += kv.Value;
        // text_rows[0].text = str; 
        // Canvas.ForceUpdateCanvases();
        // text_rows[0].font.RequestCharactersInTexture(str, text_rows[0].fontSize, text_rows[0].fontStyle);
        // text_rows[0].text = "";

        foreach (string s in Auto_Close_Pairs.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries) ) {
            Auto_Close_Pairs_Dict.Add(s[0].ToString(), s[1].ToString());
        }

        //Intellisense_Init();
        Close_Intellisense();
        if (Intellisense_Settings.suggestion_item != null) Intellisense_Settings.suggestion_item.SetActive(false);
        if (Intellisense_Settings.method_signature_text != null) Intellisense_Settings.method_signature_text.enabled = false;
        Intellisense_AddUsing("UnityEngine");
        builtin_types.Add("bool", "System.Boolean"); builtin_types.Add("byte", "System.Byte"); builtin_types.Add("sbyte", "System.SByte");
        builtin_types.Add("char", "System.SByte");  builtin_types.Add("decimal", "System.Decimal"); builtin_types.Add("double ", "System.Double");
        builtin_types.Add("float", "System.Single"); builtin_types.Add("int", "System.Int32"); builtin_types.Add("uint", "System.UInt32");
        builtin_types.Add("long", "System.Int64"); builtin_types.Add("ulong", "System.UInt64"); builtin_types.Add("object", "System.Object");
        builtin_types.Add("short", "System.Int16"); builtin_types.Add("ushort", "System.UInt16"); builtin_types.Add("string", "System.String");
        var keys = builtin_types.Keys.ToArray(); foreach (var k in keys) builtin_types.Add(k.Trim()+"[]", builtin_types[k].Trim()+"[]");
        if (Intellisense_Settings.max_suggestion_count <= 0) Intellisense_Settings.max_suggestion_count = 20;

        text = "";
        current_row = 0;
        Cursor_Update_Position();

        //Hack to use without Engine script in scene
        if (GameObject.Find("Compiler") == null && GameObject.Find("Cube_Center") == null) {
            Engine.Hotkeys.Add(Engine.Key.Set_Breakpoint, new KeyCode[][]{ new KeyCode[]{KeyCode.LeftAlt, KeyCode.B} });
            Engine.Hotkeys.Add(Engine.Key.Intellisense_Up, new KeyCode[][]{ new KeyCode[]{KeyCode.LeftAlt, KeyCode.UpArrow} });
            Engine.Hotkeys.Add(Engine.Key.Intellisense_Down, new KeyCode[][]{ new KeyCode[]{KeyCode.LeftAlt, KeyCode.DownArrow} });
            Engine.Hotkeys.Add(Engine.Key.Intellisense_Submit, new KeyCode[][]{ new KeyCode[]{KeyCode.Tab}, new KeyCode[]{KeyCode.LeftAlt, KeyCode.Return}, new KeyCode[]{KeyCode.LeftAlt, KeyCode.KeypadEnter}});
            Engine.Hotkeys.Add(Engine.Key.Intellisense_Enable, new KeyCode[][]{ new KeyCode[]{KeyCode.LeftAlt, KeyCode.LeftControl, KeyCode.I}});
            Engine.Hotkeys.Add(Engine.Key.Debug_Force_Update, new KeyCode[][]{ new KeyCode[]{KeyCode.LeftAlt, KeyCode.LeftControl, KeyCode.T}});
        }

        //Add child elements script to handle mouseOver/mouseOut of overlayed objects
        Text_Editor_Child_Element tece;
        if (ScrollH != null) { tece = ScrollH.gameObject.AddComponent<Text_Editor_Child_Element>(); tece.te = this; }
        if (ScrollV != null) { tece = ScrollV.gameObject.AddComponent<Text_Editor_Child_Element>(); tece.te = this; }
        if (Intellisense_Settings.suggestion_rt != null) {
            tece = Intellisense_Settings.suggestion_rt.gameObject.AddComponent<Text_Editor_Child_Element>(); tece.te = this;
        }
        if (Intellisense_Settings.suggestion_item != null) {
            var tesi = Intellisense_Settings.suggestion_item.gameObject.AddComponent<Text_Editor_Suggestion_Item>(); tesi.te = this;
        }

        check_var_thread.DoWork += Intellisense_var_check_bg; check_var_thread.RunWorkerAsync();

        if (Activate_Settings.Activate != Activate_Scheme_Info.Activate_Enum.OnMouseOver)
            { if (Activate_Settings.IsActive) Activate(); else DeActivate(); }
        else 
            { DeActivate(); }
    }

    // Update is called once per frame
    void Update()
    {
        UnityEvent ue;
        while (Event_Queue.TryDequeue(out ue)) {
            if (ue != null) ue.Invoke();
        }

        if (Engine.check_key(Engine.Key.Set_Breakpoint)) { SetBreakpoint(); }
        if (Engine.check_key(Engine.Key.Intellisense_Enable)) { enable_intellisense = !enable_intellisense; }
        if (Engine.check_key(Engine.Key.Intellisense_Enable)) { enable_intellisense = !enable_intellisense; }
        if (Engine.check_key(Engine.Key.Debug_Force_Update)) { Intellisense_var_check_bg(null, null); }

        //Recalculate text width on canvas size change (when game window is resized)
        if (transform.hasChanged) {
            transform.hasChanged = false;
            var cur_scale = GetComponent<RectTransform>().lossyScale;
            if (cur_scale != old_scale) { old_scale = cur_scale; Recalculate_text_width(); }
        }

        //TODO: When vertical scroll down is possible, clicking at the bottom-most line, will invoke selection, because of cursor adjust which will reinvoke scroll adjust
        //
        //CAN'T REPRODUCE: If ctrl + X longest line and HScroll > 0, hscroll size is set to full, but scrolling still on the right
        //QUESTIONABLE: Selection of carriage-return symbol (last symbol on the line) for copying/pasting/deleting
        //
        //DONE: Tab button handle
        //DONE: Clicks on the end of the line not detected, if the line is bigger then one screen
        //DONE: Caps lock status (caps lock should only uppercase letters, not symbols)
        //DONE: Backspace/Delete (including first/last char handling and another line altering)
        //DONE: Selection (mouse and shift+arrows/home/end)
        //DONE: Repeat by keyCode, not by typed char (for repeating arrows, baclspaces e.t.c.)
        //DONE: Vertical scroll
        //DONE: PgUp/PgDwn
        //DONE: Ctrl + Home/End
        //DONE: Insert mode
        //DONE: BUG: type line, press shift+home, delete, shift+end = error
        //DONE: Typing should erase selection entirely
        //DONE: Enter should erase selection
        //DONE: Copy/Cut/Paste
        //DONE: Backspace and delete should erase selection entirely
        //DONE: Arrow keys should change line when pressed at beginning/end of line
        //DONE: ctrl + X multiple lines does not delete empty lines
        //DONE: ctrl + A = select all
        //DONE: BUG: shift+up - up arrow still be repeated even when released
        //DONE: BUG: type short line and 2nd longer line. At start of line 0 select to bottom (shift+down), then press 'END' multiple times (while still holding shift). SOMETIMES it throws an error (it's related to shift+up/down repeating bug, but if down & end pressed in same frame current_row check should be done anyway)
        //DONE: Ctrl + up/down = VScroll per row
        //DONE: Ctrl + Arrows for word navigation
        //DONE: Go to the end of 2nd line (should be longer or equal then first one), press shift+up = selector shows at 0,0
        //DONE: Go to the end of 2nd line (should be longer or equal then first one), press shift+up, select some leters to the left (shift-left), now while still holding shift press 'END' - selection is not deselected
        //DONE: Go to the end of 2nd line, press shift+up, select some leters to the left. Now press right while holding shift to deselect. Last letter on this line can not be deselected.
        //DONE: Select a line to the bottom (shift+down) - first letter on this line can not be deselected
        //DONE: When select multiple rows, first/last charcter on the last/first selected row can not be deselected
        //DONE: V-Scroll size не всегда расширяется (и сужается?)
        //DONE: Mouse V-scroll
        //DONE: Click below viewport height (when scrolled to bottom a little bit) not detected
        //DONE: ctrl + Z = undo
        //DONE: Intellisense: when typing '1234A' should not give suggestion
        //DONE: Syntax higlight: higlighting 'if' in 'modification'
        //DONE: Scrollbar slider size not calculated correctly?
        //DONE: Mouse VScroll navigation should allow cursor to go out of viewport
        //DONE: Intellisense: suggestion mouse scroll
        //DONE: Intellisense: suggestion mouse click
        //DONE: Alt + mouse scroll to scroll horizontally
        //DONE: Ctrl + up/down = VScroll per row navigation should not stick cursor and allow cursor to go out of viewport
        //DONE: When requested cursor x_pos is grater then line_width, need to store requested x_pos to apply it when navigating to another line
        //DONE: Ctrl + mouse scroll to change font size
        //DONE: Triple click to select full row

        Cursor_Blink();

        //Activation / Deactivation
        #region "Activation / Deactivation OnMouseClick"
        if (Input.GetKeyDown(KeyCode.Mouse0)) {
            if (Activate_Settings.IsActive) {
                if (!mouse_is_over_me && Activate_Settings.DeActivate == Activate_Scheme_Info.DeActivate_Enum.OnClickOutside) DeActivate();
            } else {
                if (mouse_is_over_me && Activate_Settings.Activate == Activate_Scheme_Info.Activate_Enum.OnClick) Activate();
            }
        }
        if (!Activate_Settings.IsActive) return;
        #endregion

        #region "Key repeat handler"
        if (repeat_start_timer >= 0) repeat_start_timer += Time.deltaTime;
        if (repeat_ratio_timer >= 0) repeat_ratio_timer += Time.deltaTime;

        //Reset repeat timer on KeyUp
        if (repeat_start_timer >= 0) {
            if ((int)repeat_key < 1000) { if (!Input.GetKey(repeat_key)) repeat_start_timer = -1f; }
            else if ((int)repeat_key == 10001) { if (!Engine.check_key(Engine.Key.Intellisense_Up, false)) repeat_start_timer = -1f; }
            else if ((int)repeat_key == 10002) { if (!Engine.check_key(Engine.Key.Intellisense_Down, false)) { repeat_start_timer = -1f; } }
        }

        //Repeating character
        KeyCode repeat = KeyCode.None;
        if (repeat_start_timer >= repeat_start && repeat_ratio_timer >= repeat_ratio) {
            repeat_ratio_timer = 0f;
            repeat = repeat_key;
        }
        #endregion

        //If suggestion is active and suggestion key is hit - submit suggestion
        #region "Intellisense navigation"
        if (Intellisense_Settings.suggestion_rt != null && Intellisense_Settings.suggestion_rt.gameObject.activeSelf) {
            if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0) suggestion_mouse_enabled = true;
            if (Engine.check_key(Engine.Key.Intellisense_Submit)) { Intellisense_Navigation(nav_enum.Submit); return; }
            if (repeat == (KeyCode)10001 || Engine.check_key(Engine.Key.Intellisense_Up))   { Intellisense_Navigation(nav_enum.Up); suggestion_mouse_enabled = false; }
            if (repeat == (KeyCode)10002 || Engine.check_key(Engine.Key.Intellisense_Down)) { Intellisense_Navigation(nav_enum.Down); suggestion_mouse_enabled = false; }
        }
        #endregion

        bool alt_Pressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool ctrl_Pressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift_pressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        //Handle letter/number/symbols keys
        #region "Handle letter/number/symbols keys"
        string type = "";
        bool isCapsLockOn = (GetKeyState(0x14) & 1) > 0;
        bool pair_autoclosed = false;
        if (!ctrl_Pressed && !alt_Pressed) {
            foreach (var ki in keyinfo) {
                if (Input.GetKeyDown(ki.Key) || repeat == ki.Key) {
                    type = ki.Value.ToString();
                    if (shift_pressed)
                        if (keyUpper.ContainsKey(ki.Key)) type = keyUpper[ki.Key].ToString(); else type = type.ToUpper();

                    //Caps lock should only uppercase letters, and not affect symbols
                    if (isCapsLockOn)
                        if(!shift_pressed) type = type.ToUpper(); else type = type.ToLower();

                    Character_Repeat_Handler("SET", ki.Key);

                    //Autoclose pairs
                    if (Auto_Close_Pairs_Dict.ContainsValue(type) && Selection_Get_Text() == "") {
                        string t = text_rows[current_row].text;
                        if (t.Length > current_chr && t[current_chr] == type[0]) { current_chr++; Cursor_Update_Position(); return; }
                    } 
                    if (Auto_Close_Pairs_Dict.ContainsKey(type) && Selection_Get_Text() == "" ) { 
                        type += Auto_Close_Pairs_Dict[type]; pair_autoclosed = true; 
                    } 

                    break;                
                }
            }
        }
        #endregion

        //Clipboard copy/cut/paste
        #region "Clipboard copy/cut/paste"
        if (Input.GetKeyDown(KeyCode.C) && ctrl_Pressed) {
            GUIUtility.systemCopyBuffer = Selection_Get_Text();
        }
        if (Input.GetKeyDown(KeyCode.X) && ctrl_Pressed) {
            GUIUtility.systemCopyBuffer = Selection_Get_Text(true);
        }
        if (Input.GetKeyDown(KeyCode.V) && ctrl_Pressed) {
            type = GUIUtility.systemCopyBuffer.Replace("\r\n", "\n");
        }
        #endregion
        
        //Enter
        #region "Enter"
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || repeat == KeyCode.Return || repeat == KeyCode.KeypadEnter) {
            if (!alt_Pressed) {
                //If enter is pressed in the middle of the row - split row and move right part to the next row
                string sel = Selection_Get_Text(true);
                if (current_chr < text_rows[current_row].text.Length) {
                    Row_Split(current_row, current_chr);
                    Syntax_Highlight ( current_row );
                    Syntax_Highlight ( current_row + 1 );
                    if (sel == "") {
                        changed_rows.Enqueue( new changed_rows_info(text_rows[current_row], text_rows[current_row].text) );
                        changed_rows.Enqueue( new changed_rows_info(text_rows[current_row+1], text_rows[current_row+1].text) );
                    }
                } else {
                    Create_New_Row(current_row + 1);
                }
                current_row++; current_chr = 0;
                Cursor_Update_Position(); Selection_Clear(); Cursor_Blink(true); Update_Scroll_V_Size(); Update_Scroll_H_Size();
                Close_Intellisense();
                if (Input.GetKeyDown(KeyCode.Return)) Character_Repeat_Handler("SET", KeyCode.Return); else Character_Repeat_Handler("SET", KeyCode.KeypadEnter);
            } else {
                Intellisense_Navigation(nav_enum.Submit);
            }
        }
        #endregion

        //Backspace
        #region "Backspace"
        if (Input.GetKeyDown(KeyCode.Backspace) || repeat == KeyCode.Backspace) {
            if (Selection_Get_Text(true).Length == 0) {
                if (current_chr > 0) {
                    Undo_Add( undo_op.modify, current_row );

                    current_chr--;
                    text_rows[current_row].text = text_rows[current_row].text.Remove(current_chr, 1);

                    float w = text_char_w[current_row][current_chr];
                    text_char_w[current_row].RemoveAt(current_chr);
                    text_rows_w[current_row] -= w;
                } else if (current_row > 0) {
                    current_row--;
                    Undo_Add( undo_op.modify, current_row );

                    current_chr = text_char_w[current_row].Count();
                    text_rows[current_row].text += text_rows[current_row+1].text;

                    text_char_w[current_row].AddRange(text_char_w[current_row+1]);
                    text_rows_w[current_row] += text_rows_w[current_row+1];

                    Row_Erase(current_row + 1);
                }
                Syntax_Highlight ( current_row );
                changed_rows.Enqueue( new changed_rows_info(text_rows[current_row], text_rows[current_row].text) );
            }

            Cursor_Update_Position(); Selection_Clear(); Cursor_Blink(true); Update_Scroll_V_Size(); Update_Scroll_H_Size();
            Close_Intellisense();
            Character_Repeat_Handler("SET", KeyCode.Backspace);
        }
        #endregion

        //Delete
        #region "Delete"
        if (Input.GetKeyDown(KeyCode.Delete) || repeat == KeyCode.Delete) {
            if (Selection_Get_Text(true).Length == 0) {
                if (current_chr < text_char_w[current_row].Count()) {
                    Undo_Add( undo_op.modify, current_row );
                    text_rows[current_row].text = text_rows[current_row].text.Remove(current_chr, 1);

                    float w = text_char_w[current_row][current_chr];
                    text_char_w[current_row].RemoveAt(current_chr);
                    text_rows_w[current_row] -= w;
                } else if (current_row < text_rows_w.Count()-1) {
                    Undo_Add( undo_op.modify, current_row );
                    Undo_Add( undo_op.modify, current_row + 1 );
                    text_rows[current_row].text += text_rows[current_row+1].text;

                    text_char_w[current_row].AddRange(text_char_w[current_row+1]);
                    text_rows_w[current_row] += text_rows_w[current_row+1];

                    Row_Erase(current_row + 1);
                }
                Syntax_Highlight ( current_row );
                changed_rows.Enqueue( new changed_rows_info(text_rows[current_row], text_rows[current_row].text) );
            }

            Cursor_Update_Position(); Selection_Clear(); Cursor_Blink(true); Update_Scroll_V_Size(); Update_Scroll_H_Size();
            Close_Intellisense();
            Character_Repeat_Handler("SET", KeyCode.Delete);
        }
        #endregion

        //Undo
        #region "Undo"
        if (Input.GetKeyDown(KeyCode.Z) && ctrl_Pressed) {
            Undo_Restore();
            Cursor_Update_Position(); Selection_Clear(); Cursor_Blink(true); Update_Scroll_V_Size(); Update_Scroll_H_Size();
            Close_Intellisense();
            if (OnTextChanged != null) OnTextChanged(this, new EventArgs());
        }
        #endregion

        //Arrows/Home/End navigation and Mouse scroll
        #region "Arrows/Home/End navigation and Mouse scroll"
        if (Input.GetKeyDown(KeyCode.LeftArrow)  || repeat == KeyCode.LeftArrow)  { Navigate(nav_enum.Left); }
        if (Input.GetKeyDown(KeyCode.RightArrow) || repeat == KeyCode.RightArrow) { Navigate(nav_enum.Right); }
        if (Input.GetKeyDown(KeyCode.UpArrow)    || repeat == KeyCode.UpArrow)    { if (!alt_Pressed) Navigate(nav_enum.Up); }
        if (Input.GetKeyDown(KeyCode.DownArrow)  || repeat == KeyCode.DownArrow)  { if (!alt_Pressed) Navigate(nav_enum.Down); }
        if (Input.GetKeyDown(KeyCode.PageUp)     || repeat == KeyCode.PageUp)     { Navigate(nav_enum.PgUp); }
        if (Input.GetKeyDown(KeyCode.PageDown)   || repeat == KeyCode.PageDown)   { Navigate(nav_enum.PgDn); }
        //if (Input.GetKeyDown(KeyCode.Home))                                       { Navigate(nav_enum.Home); }
        //if (Input.GetKeyDown(KeyCode.End))                                        { Navigate(nav_enum.End); }
        //if (Input.GetKeyDown(KeyCode.Home) && ctrl_Pressed)                       { Navigate(nav_enum.Ctrl_Home); }
        //if (Input.GetKeyDown(KeyCode.End) && ctrl_Pressed)                        { Navigate(nav_enum.Ctrl_End); }
        if (Input.mouseScrollDelta.y != 0f) { 
            if (ctrl_Pressed && allow_fontsize_change) { 
                font_size += Mathf.RoundToInt(Input.mouseScrollDelta.y); 
                if (font_size < fontsize_change_limit.x) font_size = fontsize_change_limit.x;
                if (font_size > fontsize_change_limit.y) font_size = fontsize_change_limit.y;

                if(font_size >= 40 && font_size <=60)
                {

                }
                Update_Text_Size(); 
            }
            else if (!alt_Pressed) { Navigate(nav_enum.Mouse_Scroll); }
            else              { Navigate(nav_enum.Mouse_ScrollH); }
        }
        #endregion

        //Select all
        if (ctrl_Pressed && Input.GetKeyDown(KeyCode.A)) {
            current_row = text_rows_w.Count-1;
            current_chr = text_rows[current_row].text.Length;
            selection_start = new int[] {0,0};
            selection_end = new int[]{ current_row, current_chr };
            Cursor_Update_Position();
            Selection_Draw(selection_start, selection_end);
            Close_Intellisense();
        }

        //Mode
        if (Input.GetKeyDown(KeyCode.Insert)) {
            insert_mode = !insert_mode;
            Cursor_Update_Position(); Cursor_Blink(true);            
        }

        //Mouse click
        #region "Mouse positioning and selectioning"
        if (Input.GetKeyDown(KeyCode.Mouse0) && !mouse_is_over_my_child) {
            int[] coord = Char_Index_Under_Mouse();
            if (coord[0] >= 0 && coord[1] >= 0) {
                current_row = coord[0];
                current_chr = coord[1];
                Cursor_Update_Position(); Cursor_Blink(true);

                ready_to_select = true;
                if (!shift_pressed) selection_start = coord;
                else { selection_end = coord; Selection_Draw(selection_start, selection_end); }

                Close_Intellisense();

                //Dbl_click
                if (Time.time <= click_info.last_time + dbl_click_max_delay) {
                    if (Vector2.Distance(Input.mousePosition, click_info.last_pos) <= dbl_click_max_offset) {
                        ready_to_select = false; //Deactivate mouse selection

                        int start = 0; int end = 0;
                        if (!click_info.dbl_clicked) {
                            //Dblclick
                            start = Navigate_find_word_boundary(current_chr, current_row, -1);
                            end = Navigate_find_word_boundary(current_chr, current_row, 1);
                            //Debug.Log("Word boundary of " + current_chr + " is " + start + "x" + end);
                        } else {
                            //Tripleclick
                            end = text_rows[current_row].text.Length;
                        }
                        click_info.dbl_clicked = true;

                        current_chr = end;
                        selection_start = new int[]{current_row, start};
                        selection_end = new int[]{current_row, end};

                        Cursor_Update_Position(); Cursor_Blink(true); 
                        Selection_Draw(selection_start, selection_end);
                    }
                } else {
                    click_info.dbl_clicked = false;
                }
                click_info.last_time = Time.time;
                click_info.last_pos = Input.mousePosition;
            }
        }
        if (Input.GetKeyUp(KeyCode.Mouse0)) {
            ready_to_select = false;
        }
        if (ready_to_select) {
            int[] coord = Char_Index_Under_Mouse();
            if (coord[0] >= 0 && coord[1] >= 0) {
                current_row = coord[0];
                current_chr = coord[1];
                Cursor_Update_Position(); Cursor_Blink(true);

                selection_end = coord;
                Selection_Draw(selection_start, selection_end);
            }
        }
        #endregion

        //Tab with multiline selection or shift+tab
        if (Input.GetKeyDown(KeyCode.Tab) && (selection_start[0] != selection_end[0] || shift_pressed)) {
            int tab_length = 3;
            var cur_position = new int[]{current_row, current_chr};
            int[] s = selection_start; int[] e = selection_end;
            bool dont_select_full_rows = false;
            Selection_Clear();
            if (s[0] > e[0]) { int[] tmp = e; e = s; s = tmp; }
            if (!shift_pressed) {
                for (int i = s[0]; i <= e[0]; i++) {
                    current_row = i; current_chr = 0; type_text("\t", true);
                }
                current_row = cur_position[0];
                current_chr = cur_position[1] + tab_length;
            } else {
                int count = 0;
                for (int i = s[0]; i <= e[0]; i++) {
                    Undo_Add( undo_op.modify, current_row );
                    count = text_rows[i].text.TakeWhile(Char.IsWhiteSpace).Count();
                    if (count > tab_length) count = tab_length;

                    text_rows[i].text = text_rows[i].text.Remove(0, count);

                    float w = text_char_w[i].Skip(current_chr).Take(count).Sum();
                    text_char_w[i].RemoveRange(0, count);
                    text_rows_w[i] -= w;
                    Syntax_Highlight ( i );

                    if (i == current_row) {
                        if (current_chr >= count) current_chr -= count; else current_chr = 0;
                    }
                }

                //If we had only one row selected - try to restore selection
                if (s[0] == e[0]) {
                    if (s[1] >= count) { s[1] -= count; } else { s[1] = 0; }
                    if (e[1] >= count) { e[1] -= count; } else { e[1] = 0; }
                    selection_start = s; selection_end = e; dont_select_full_rows = true;
                }
            }

            if (!dont_select_full_rows) {
                selection_start = new int[]{s[0], 0};
                selection_end = new int[]{e[0], text_char_w[e[0]].Count};
            }
            Selection_Draw(selection_start, selection_end);
            Cursor_Update_Position(); Update_Scroll_V_Size(); Update_Scroll_H_Size();
        }
        else if (type != "") { 
            type_text(type);
            if (pair_autoclosed) { current_chr--; Cursor_Update_Position(); }
        }
    }

    void type_text(string type, bool no_update = false) {
        Cursor_Blink(true); Selection_Get_Text(true);

        type = type.Replace("\t", new string(' ', tab_spaces));

        string[] lines = type.Split(new char[]{'\r','\n'});
        for (int n = 0; n < lines.Count(); n++) {
            Undo_Add(undo_op.modify, current_row);

            //Debug.Log("current row: " + current_row + ", cur_chr: " + current_chr + ", rows count: " + text_rows.Count + ", inserting line: " + n);
            text_rows[current_row].text = text_rows[current_row].text.Insert(current_chr, lines[n]);
            if (insert_mode && lines[n].Length == 1 && text_rows[current_row].text.Length > current_chr + 1) {
                text_rows[current_row].text = text_rows[current_row].text.Remove(current_chr + 1, 1);
            }
            Syntax_Highlight ( current_row );

            //DONE: - calling ForceUpdateCanvases is needed because a character needs to be rendered before we can get width
            //        BUT, instead of calling ForceUpdateCanvases every typed character,
            //        will be better to render all available character once at start

            canvas_scale = text_rows[current_row].canvas.transform.localScale.x;
            if (text_rows[current_row].canvas.renderMode == RenderMode.WorldSpace) canvas_scale = 1f;
            scaled_font_size = Mathf.FloorToInt(text_rows[current_row].fontSize * canvas_scale);
            text_rows[current_row].font.RequestCharactersInTexture(lines[n], scaled_font_size, text_rows[current_row].fontStyle);

            foreach(char chr in lines[n]) {
                CharacterInfo ci = new CharacterInfo();

                text_rows[current_row].font.GetCharacterInfo(chr, out ci, scaled_font_size, text_rows[current_row].fontStyle );
                float advance = ci.advance / canvas_scale;
                text_char_w[current_row].Insert(current_chr, advance );
                text_rows_w[current_row] += advance;

                //Debug.Log("canvas_scale: " + canvas_scale + ", scaled_font_size: " + scaled_font_size + ", char '" + chr + "' width = " + ci.advance + ", scaled width = " + advance);
                current_chr += 1;
            }

            changed_rows.Enqueue( new changed_rows_info(text_rows[current_row], text_rows[current_row].text) );

            //If it's not last row - create new one
            if (n != lines.Count() - 1) {
                if (current_chr < text_rows[current_row].text.Length) Row_Split(current_row, current_chr);
                else  Create_New_Row(current_row + 1);

                current_row++; current_chr = 0;
            }
        }

        if (!no_update) {
            if (type.Length == 1 && lines.Count() == 1) Intellisense(text_rows[current_row].text, current_chr - 1);
            Cursor_Update_Position(); Selection_Clear(); Update_Scroll_V_Size(); Update_Scroll_H_Size();
        }
    }

    #region Navigation
    void Navigate (nav_enum nav)
    {
        bool dont_update_previous = false;
        bool shift_pressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool ctrl_Pressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (nav == nav_enum.Left && ctrl_Pressed) nav = nav_enum.Word_Left;
        if (nav == nav_enum.Right && ctrl_Pressed) nav = nav_enum.Word_Right;
        if (nav == nav_enum.Up && ctrl_Pressed) nav = nav_enum.Row_Up;
        if (nav == nav_enum.Down && ctrl_Pressed) nav = nav_enum.Row_Down;

        if (nav == nav_enum.Left) {
            current_chr--; Character_Repeat_Handler("SET", KeyCode.LeftArrow);
            if (current_chr < 0 && current_row > 0) {
                current_row--; current_chr = text_rows[current_row].text.Length;
            }
        }
        else if (nav == nav_enum.Right) {
            current_chr++; Character_Repeat_Handler("SET", KeyCode.RightArrow);
            if (current_chr > text_rows[current_row].text.Length && current_row < text_rows.Count-1) {
                current_row++; current_chr = 0;
            }
        }
        else if (nav == nav_enum.Up) {
            dont_update_previous = true;
            current_row--; Character_Repeat_Handler("SET", KeyCode.UpArrow);
        }
        else if (nav == nav_enum.Down) {
            dont_update_previous = true;
            current_row++; Character_Repeat_Handler("SET", KeyCode.DownArrow);
        }
        else if (nav == nav_enum.PgUp) {
            current_row-= Mathf.FloorToInt(txt_cnt_rt.rect.height / lineHeight); Character_Repeat_Handler("SET", KeyCode.PageUp);
        }
        else if (nav == nav_enum.PgDn) {
            current_row+= Mathf.FloorToInt(txt_cnt_rt.rect.height / lineHeight); Character_Repeat_Handler("SET", KeyCode.PageDown);
        }
        else if (nav == nav_enum.Home) {
            int first_non_space = text_rows[current_row].text.TakeWhile(c => char.IsWhiteSpace(c)).Count();
            if (current_chr == first_non_space) current_chr = 0;
            else current_chr = first_non_space;
            //Old behaviour
            //current_chr=0;
        }
        else if (nav == nav_enum.End) {
            current_chr = text_rows[current_row].text.Length;
        }
        else if (nav == nav_enum.Ctrl_Home) {
            current_chr=0; current_row = 0;
        }
        else if (nav == nav_enum.Ctrl_End) {
            current_row = text_rows_w.Count-1; current_chr = text_rows[current_row].text.Length;
        }
        else if (nav == nav_enum.Word_Left) {
            current_chr = Navigate_find_word_boundary(current_chr, current_row, -1);
        }
        else if (nav == nav_enum.Word_Right) {
            current_chr = Navigate_find_word_boundary(current_chr, current_row, 1);
        }
        else if (nav == nav_enum.Row_Up) {
            float scroll_value = (text_rows_w.Count * lineHeight) - txt_cnt_rt.rect.height;
            float new_val = cur_scroll_V - (lineHeight / scroll_value);
            if (new_val < 0f) new_val = 0f;
            if (ScrollV != null) ScrollV.value = new_val;
            On_Scroll_V(new_val);
            Character_Repeat_Handler("SET", KeyCode.UpArrow);
            return; //do not update cursor position, because it will reset view to where cursor is
        }
        else if (nav == nav_enum.Row_Down) {
            float scroll_value = (text_rows_w.Count * lineHeight) - txt_cnt_rt.rect.height;
            float new_val = cur_scroll_V + (lineHeight / scroll_value);
            if (new_val > 1f) new_val = 1f;
            if (ScrollV != null) ScrollV.value = new_val;
            On_Scroll_V(new_val);
            Character_Repeat_Handler("SET", KeyCode.DownArrow);
            return; //do not update cursor position, because it will reset view to where cursor is
        }
        else if (nav == nav_enum.Mouse_Scroll) {
            if (mouse_is_over_my_child) return;

            int COEFF = 3; //Rows per scroll step
            float y = Input.mouseScrollDelta.y;
            float new_val = cur_scroll_V;
            if (y < 0) {
                float h = Mathf.Abs(y) * COEFF * lineHeight;
                float scroll_value = (text_rows_w.Count * lineHeight) - txt_cnt_rt.rect.height;
                new_val = cur_scroll_V + (h / scroll_value);
            } else if (y > 0) {
                float h = y * COEFF * lineHeight;
                float scroll_value = (text_rows_w.Count * lineHeight) - txt_cnt_rt.rect.height;
                new_val = cur_scroll_V - (h / scroll_value);
            }

            if (new_val < 0f) new_val = 0f;
            if (new_val > 1f) new_val = 1f;
            if (ScrollV != null) ScrollV.value = new_val;
            On_Scroll_V(new_val);
            return; //do not update cursor position, because it will reset view to where cursor is
        } else if (nav == nav_enum.Mouse_ScrollH) {
            if (mouse_is_over_my_child) return;
            int COEFF = font_size; //Pixels per scroll step
            float txt_w = (float)text_rows_w.Max();
            float cnt_w = txt_cnt_rt.rect.width;
            float scroll_value = Mathf.InverseLerp(0f, txt_w - cnt_w, COEFF);

            float new_value = cur_scroll_H + (-Input.mouseScrollDelta.y * scroll_value);

            if (ScrollH != null) ScrollH.value = new_value; 
            On_Scroll_H(new_value);
            return; //do not update cursor position, because it will reset view to where cursor is
        }

        Cursor_Update_Position(dont_update_previous); Cursor_Blink(true);
        
        if (shift_pressed) {
            selection_end = new int[]{current_row, current_chr};
            Selection_Draw(selection_start, selection_end);
        } else {
            Selection_Clear();
        }

        Close_Intellisense();
    }

    int Navigate_find_word_boundary(int from, int row, int direction)
    {
        int group_first = -1; //-1 space and carriage ret, 0 - letters, 1 - symbols
        string txt = text_rows[row].text;

        if (direction < 0) {
            //Left direction
            if (from <= 1) return 0;

            for (int c = from - 1; c >= 0; c--) {
                char chr = txt[c];
                if (group_first == -1) {
                    group_first = Navigate_GetCharGroup(chr);
                    if (group_first == -1) continue;
                    if (c > 0) {
                        int group_next = Navigate_GetCharGroup(txt[c-1]);
                        if (group_next == -1) { from = c; break; }
                        if (group_first == 0 && group_next > 0) group_first = group_next; //WHY??? - Because in '.a' when put cursor in between and press ctrl+left - won't move the cursor
                    }
                } else {
                    if (chr == ' ') { from = c + 1; break; }
                    //if (c == 0) { from = 0; break; }
                    int group_cur = Navigate_GetCharGroup(chr);
                    if (group_cur != group_first) { from = c + 1; break; }
                }
                if (c == 0) { from = 0; break; }
            }
        }
        else if (direction > 0) {
            //Right direction
            if (from >= txt.Length - 1) return txt.Length;

            for (int c = from; c < txt.Length; c++) {
                char chr = txt[c];
                if (group_first == -1) {
                    group_first = Navigate_GetCharGroup(chr);
                    if (group_first == -1) continue;
                    if (c < txt.Length - 1) {
                        int group_next = Navigate_GetCharGroup(txt[c+1]);
                        if (group_next == -1) { from = c+1; break; }
                        if (group_first > 0 && group_next == 0) group_first = group_next;
                    }
                } else {
                    if (chr == ' ') { from = c; break; }
                    //if (c == txt.Length - 1) { from = c+1; break; }
                    int group_cur = Navigate_GetCharGroup(chr);
                    if (group_cur != group_first) { from = c; break; }
                }
                if (c == txt.Length - 1) { from = txt.Length; break; }
            }
        }

        return from;
    }

    int Navigate_GetCharGroup(char c)
    {
        if (c == ' ' || c == '\r' || c == '\n' ) return -1;
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_') return 0;
        return 1;
    }
    #endregion

    #region Cursor
    void Cursor_Blink(bool reset = false) {
        if (!Activate_Settings.IsActive) {
            if (Activate_Settings.Hide_Cursor_When_Inactive) { cursor.SetActive(false); return; }
        } 

        if (reset) {
            cursor.SetActive(true);
            cursor_blink_timer = 0f;
        }

        cursor_blink_timer += Time.unscaledDeltaTime;
        if (cursor_blink_timer >= cursor_blink_rate) {
            cursor_blink_timer = 0f;
            cursor.SetActive(!cursor.activeSelf);
        }
    }

    void Cursor_Update_Position(bool dont_update_previous = false) {
        if (dont_update_previous) current_chr = current_chr_previous;
        else                      current_chr_previous = current_chr;

        if (current_row < 0) current_row = 0;
        if (current_row > text_rows.Count-1) current_row = text_rows.Count-1;
        if (current_chr < 0) current_chr = 0;
        if (current_chr > text_rows[current_row].text.Length) current_chr = text_rows[current_row].text.Length;

        float w_sum = text_char_w[current_row].GetRange(0, current_chr).Sum();
        float row_offset_px = current_row * lineHeight;
        RectTransform cursor_rt = cursor.GetComponent<RectTransform>();

        if (insert_mode) {
            float w = current_chr < text_char_w[current_row].Count ? text_char_w[current_row][current_chr] : 10;
            cursor_rt.sizeDelta = new Vector2((float)w, 5f);
            cursor_rt.anchoredPosition = new Vector2((float)w_sum, -row_offset_px - (lineHeight - 5));
        } else {
            cursor_rt.sizeDelta = new Vector2(cursor_width, lineHeight);
            cursor_rt.anchoredPosition = new Vector2((float)w_sum, -row_offset_px);
        }

        //If cursor out of viewport VERICAL - scroll to cursor
        float needed_y = float.MinValue;
        if (row_offset_px + lineHeight + 5 > txt_cnt_rt.rect.height + txt_cnt_rt.anchoredPosition.y) needed_y = row_offset_px + lineHeight + 5 - txt_cnt_rt.rect.height;
        if (row_offset_px < txt_cnt_rt.anchoredPosition.y) needed_y = row_offset_px;
        if (needed_y > float.MinValue) {
            float scroll_value = (text_rows_w.Count * lineHeight) - txt_cnt_rt.rect.height;
            float v = needed_y / scroll_value;
            if (v > 1f) v = 1f; if (v < 0f) v = 0f;
            if (ScrollV != null) ScrollV.value = v;
            On_Scroll_V(v);
        }

        //If cursor out of viewport HORIZONTAL - scroll to cursor
        float needed_x = float.MinValue;
        if (w_sum > txt_cnt_rt.rect.width + -txt_cnt_rt.anchoredPosition.x) needed_x = (float)w_sum + 5 - txt_cnt_rt.rect.width;
        if (w_sum < -txt_cnt_rt.anchoredPosition.x) needed_x = (float)w_sum - 5;
        if (needed_x > float.MinValue) {
            float max_txt_w = (float)text_rows_w.Max();
            float scroll_value = max_txt_w - txt_cnt_rt.rect.width;
            float v = needed_x / scroll_value;
            if (v > 1f) v = 1f; if (v < 0f) v = 0f;
            if (ScrollH != null) ScrollH.value = v;
            On_Scroll_H(v);
        }

        //Move suggestion box
        if (Intellisense_Settings.suggestion_rt != null) {
            Intellisense_Settings.suggestion_rt.position = cursor_rt.position;
            Vector2 pos = Intellisense_Settings.suggestion_rt.anchoredPosition += new Vector2(15f, -5f);

            Vector2 localPoint_LT, localPoint_RB;
            Vector3[] v = new Vector3[4]; //0 - Bottom-Left, 1 - Top-Left, 3 - Bottom-Right
            var sug_parent = Intellisense_Settings.suggestion_rt.transform.parent.GetComponent<RectTransform>();
            Vector2 sug_parent_pivot_offset = new Vector2(sug_parent.rect.width * sug_parent.pivot.x, -sug_parent.rect.height * sug_parent.pivot.y);
            txt_cnt_rt.GetWorldCorners(v);
            Vector2 screenP_LT = RectTransformUtility.WorldToScreenPoint( null, v[1] );
            Vector2 screenP_RB = RectTransformUtility.WorldToScreenPoint( null, v[3] );
            RectTransformUtility.ScreenPointToLocalPointInRectangle( sug_parent, screenP_LT, null, out localPoint_LT );
            RectTransformUtility.ScreenPointToLocalPointInRectangle( sug_parent, screenP_RB, null, out localPoint_RB );
            //Debug.Log("LT - w: " + v[1] + " s: " + screenP_LT.x + " " + screenP_LT.y + " l: " + localPoint_LT.x + " " + localPoint_LT.y);
            //Debug.Log("RB - w: " + v[3] + " s: " + screenP_RB.x + " " + screenP_RB.y + " l: " + localPoint_RB.x + " " + localPoint_RB.y);
            //Debug.Log("Pivot " + sug_parent_pivot_offset);
            localPoint_LT += sug_parent_pivot_offset;
            localPoint_RB += sug_parent_pivot_offset;
            //Debug.Log("Recalculated with offset: " + localPoint_LT + " - " + localPoint_RB);

            Vector2 new_pos = pos;
            float limit_X = localPoint_RB.x - Intellisense_Settings.suggestion_rt.rect.width - 5f;
            float limit_Y = localPoint_RB.y + Intellisense_Settings.suggestion_rt.rect.height + 5f;
            if (new_pos.x > limit_X) { new_pos.x = limit_X; new_pos.y -= lineHeight; }
            if (new_pos.y < limit_Y) { new_pos.y = pos.y + Intellisense_Settings.suggestion_rt.rect.height + 10f; }

            Intellisense_Settings.suggestion_rt.anchoredPosition = new_pos;
        }
    }
    #endregion

    #region Row_Manipulators
    void Create_New_Row(int at = -1) {
        if (at == -1) at = text_rows.Count;
        else {
            //Inserting row - move all rows below one way down
            for (int i = text_rows.Count-1; i >= at; i--) {
                text_rows[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -((i+1) * lineHeight));
                colored_text[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -((i+1) * lineHeight));
            }
        }

        GameObject t = Instantiate(text_template, txt_cnt);
        t.name = "TextRow" + text_rows.Count.ToString();
        t.SetActive(true);
        t.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -(at * lineHeight));
        text_rows.Insert(at, t.GetComponent<Text>());
        text_rows_w.Insert(at, 0);
        text_char_w.Insert(at, new List<float>());

        //Create colored text for code highlight
        GameObject tc = Instantiate(text_template, txt_cnt);
        tc.name = "TextRow_Colored" + at.ToString();
        tc.GetComponent<RectTransform>().anchoredPosition = text_rows[at].GetComponent<RectTransform>().anchoredPosition;
        colored_text.Insert(at, tc.GetComponent<Text>());

        if (text_rows.Count > 1) Undo_Add(undo_op.add, at); //Don't add to undo creation of first line
    }

    void Row_Split(int row, int chr)
    {
        Create_New_Row(row + 1);

        Undo_Add(undo_op.modify, row);
        Undo_Add(undo_op.modify, row+1);

        text_rows[row+1].text = text_rows[row].text.Substring(chr);
        text_rows[row].text = text_rows[row].text.Substring(0, chr);

        text_char_w[row+1].AddRange(text_char_w[row].Skip(chr));
        text_char_w[row].RemoveRange(chr, text_char_w[row].Count - chr);

        text_rows_w[row+1] = text_char_w[row+1].Sum();
        text_rows_w[row] = text_char_w[row].Sum();
    }

    void Row_Erase(int row)
    {
        Undo_Add(undo_op.remove, row);

        if (breakpoints.ContainsKey(text_rows[row])) {
            Destroy(breakpoints[text_rows[row]]);
            breakpoints.Remove(text_rows[row]);
        }

        for (int i = row + 1; i < text_rows.Count(); i++) {
            text_rows[i].GetComponent<RectTransform>().anchoredPosition += new Vector2(0f, lineHeight);
            colored_text[i].GetComponent<RectTransform>().anchoredPosition += new Vector2(0f, lineHeight);
        }

        Destroy(text_rows[row].gameObject);
        text_rows.RemoveAt(row);
        text_char_w.RemoveAt(row);
        text_rows_w.RemoveAt(row);

        //Destroy highlighted text
        Destroy( colored_text[row].gameObject );
        colored_text.RemoveAt(row);
    }
    #endregion

    #region Selection_Handlers
    void Selection_Clear() {
        //sel_start = sel_start ?? new int[]{0,0}; //If null set to {0,0}
        selection_start = new int[]{current_row, current_chr};
        selection_end = new int[]{current_row, current_chr};
        if (selector_per_row.Count() > 0) Selection_Draw(new int[]{0,0}, new int[]{0,0}); //Hide all selectors
    }

    void Selection_Draw(int[] start, int[] end) {
        //TODO: сейчас у меня переменные selectio_start и selection_end устанавливаются каждый раз перед вызовом Selection_Draw
        //      нужно реально это действие загнать сюда

        //Debug.Log("Select from " + start[0].ToString() + ":" + start[1].ToString() + " to " + end[0].ToString() + ":" + end[1].ToString());

        if (start[0] == end[0] && start[1] == end[1]) {
            //If start selection == end - just destroy all selectors
            foreach (var kv in selector_per_row) Destroy(kv.Value.gameObject);
            selector_per_row.Clear();
            return;
        }

        int from = 0, to = 0;
        List<int> used_selectors = new List<int>();
        if (start[0] != end[0]) {
            //Multiple lines (select one by one)
            if (start[0] > end[0]) { int[] t = start; start = end; end = t; }
            for (int l = start[0]; l <= end[0]; l++) {
                if      (l == start[0]) { from = start[1]; to = text_char_w[l].Count(); }
                else if (l == end[0])   { from = 0;        to = end[1];                 }
                else                    { from = 0;        to = text_char_w[l].Count(); }

                bool select_empty = false;
                //if (to == 0) select_empty = true;
                if (text_char_w[l].Count() == 0) select_empty = true;
                if ( Selection_Draw_SingleLine(l, from, to, select_empty) ) used_selectors.Add(l);
            }
        } else {
            //Select single line
            if (Selection_Draw_SingleLine( start[0], start[1], end[1]) ) used_selectors.Add(start[0]);
        }

        //Destroy unused selectors
        List<int> to_remove = new List<int>();
        foreach (var kv in selector_per_row) {
            if ( !used_selectors.Contains(kv.Key) ) { Destroy(kv.Value.gameObject); to_remove.Add(kv.Key); }
        }
        foreach (int k in to_remove) selector_per_row.Remove(k);
    }
    bool Selection_Draw_SingleLine(int row, int from, int to, bool select_empy_line = false) {
        //Get current row selector
        if (!select_empy_line && from == to) return false;

        RectTransform cur_row_sel = null;
        if (selector_per_row.ContainsKey(row)) {
            cur_row_sel = selector_per_row[row];
        } else {
            GameObject g = Instantiate(selector, selector.transform.parent);
            g.SetActive(true);
            cur_row_sel = g.GetComponent<RectTransform>();
            selector_per_row.Add(row, cur_row_sel);
        }

        //if (!select_empy_line && from == to) return;
        //Debug.Log("row: " + row + ", from: " + from + ", to: " + to + ", text_char_w[row].count:" + text_char_w.Count());
        float w_sum = text_char_w[row].GetRange(0, from).Sum();
        float w_sum_end = text_char_w[row].GetRange(0, to).Sum();

        if (select_empy_line) w_sum_end = 10;
        if (w_sum > w_sum_end) { float t = w_sum; w_sum = w_sum_end; w_sum_end = t; }

        cur_row_sel.anchoredPosition = new Vector2((float)w_sum, -(row * lineHeight));
        cur_row_sel.sizeDelta = new Vector2(w_sum_end - w_sum, lineHeight);

        return true;
    }

    string Selection_Get_Text(bool delete = false)
    {
        string t = "";
        if (selection_start[0] != selection_end[0] || selection_start[1] != selection_end[1]) {
            int[] from = selection_start;
            int[] to = selection_end;

            //Debug.Log("1: " + from[0] + "x" + from[1] + " : " + to[0] + "x" + to[1]);

            if      (from[0] == to[0]) { if (from[1] > to[1]) { int[] tmp = to; to = from; from = tmp; } }
            else if (from[0] > to[0])  { int[] tmp = to; to = from; from = tmp; }

            //Debug.Log("2: " + from[0] + "x" + from[1] + " : " + to[0] + "x" + to[1]);

            for (int line = from[0]; line <= to[0]; line++) {
                if (line >= text_rows.Count) break;
                int char_from = 0;
                int char_to = text_char_w[line].Count;
                if (line == from[0]) char_from = from[1];
                if (line == to[0]) char_to = to[1];
                if (char_to < char_from) { int tmp = char_from; char_from = char_to; char_to = tmp; }
                t += text_rows[line].text.Substring(char_from, char_to - char_from) + "\r\n";

                if (delete) {
                    if (line == from[0] || line == to[0]) {
                        Undo_Add(undo_op.modify, line);
                        int count = char_to - char_from;
                        text_rows[line].text = text_rows[line].text.Remove(char_from, count);
                        text_rows_w[line] -= text_char_w[line].Skip(char_from).Take(count).Sum();
                        text_char_w[line].RemoveRange(char_from, count);
                    }
                }
            }
            t = t.Substring(0, t.Length-2);

            if (delete) { 
                current_row = from[0]; current_chr = from[1]; 
                Cursor_Update_Position(); Selection_Clear();

                //if multiline - merge first and last lines and erase all others
                if (from[0] != to[0]) {
                    Undo_Add(undo_op.modify, from[0]);
                    text_rows[from[0]].text += text_rows[to[0]].text;

                    text_char_w[from[0]].AddRange(text_char_w[to[0]]);
                    text_rows_w[from[0]] += text_rows_w[to[0]];

                    for (int l = to[0]; l > from[0]; l--) Row_Erase(l);

                    Update_Scroll_V_Size();
                }

                Update_Scroll_H_Size();
                Syntax_Highlight(from[0]);
                changed_rows.Enqueue( new changed_rows_info(text_rows[from[0]], text_rows[from[0]].text) );
            }
        }
        return t;
    }
    #endregion

    #region Scrolling_Handlers
    void OnRectTransformDimensionsChange() {
        if (txt_cnt_rt!= null) {
            var cur_size = txt_cnt_rt.rect.size;
            if (cur_size.x != old_size.x) Update_Scroll_H_Size();
            if (cur_size.y != old_size.y) Update_Scroll_V_Size();
            old_size = cur_size;
        }
    }

    void Update_Scroll_H_Size() {
        //H-ScrollBar
        if (ScrollH != null) {
            float cnt_w = txt_cnt_rt.rect.width;
            float txt_w = (float)text_rows_w.Max();
            if (cnt_w >= txt_w) {
                ScrollH.size = 1f;
            } else {
                ScrollH.size = 1f / (txt_w / cnt_w);
            }
        }
        On_Scroll_H(cur_scroll_H);
    }

    void Update_Scroll_V_Size() {
        //V-ScrollBar
        if (ScrollV != null) {
            float cnt_h = txt_cnt_rt.rect.height;
            float txt_h = (float)text_rows_w.Count * lineHeight;
            if (cnt_h >= txt_h) {
                ScrollV.size = 1f;
            } else {
                ScrollV.size = 1f / (txt_h / cnt_h);
            }
        }
        On_Scroll_V(cur_scroll_V);
    }

    void On_Scroll_H(float v) {
        cur_scroll_H = v;
        float cnt_w = txt_cnt_rt.rect.width;
        float txt_w = (float)text_rows_w.Max();
        if (cnt_w >= txt_w) {
            txt_cnt_rt.anchoredPosition = new Vector2(0f, txt_cnt_rt.anchoredPosition.y);
        } else {
            float max_offset = txt_w - cnt_w;
            v = Mathf.Lerp(0f, max_offset, v);
            txt_cnt_rt.anchoredPosition = new Vector2(-v, txt_cnt_rt.anchoredPosition.y);
            //Debug.Log("cnt_w: " + cnt_w.ToString() + ", txt_w: " + txt_w.ToString() + ", max_offset: " + max_offset.ToString());
        }
    }

    void On_Scroll_V(float v) {
        cur_scroll_V = v;
        float cnt_h = txt_cnt_rt.rect.height;
        float txt_h = (float)text_rows_w.Count * lineHeight;
        if (cnt_h >= txt_h) {
            txt_cnt_rt.anchoredPosition = new Vector2(txt_cnt_rt.anchoredPosition.x, 0f);
        } else {
            float max_offset = txt_h - cnt_h;
            v = Mathf.Lerp(0f, max_offset, v);
            txt_cnt_rt.anchoredPosition = new Vector2(txt_cnt_rt.anchoredPosition.x, v);
        }
    }
    #endregion

    #region Undo_Handlers
    bool restoring_undo = false;

    void Undo_Add( undo_op op, int ind ) {
        if (restoring_undo) return;

        float now = Time.time;

        Undo_struct u = new Undo_struct();
        u.operation = op;
        u.line_ind = ind;
        u.txt = text_rows[ind].text;
        u.cursor_pos = new Vector2Int(current_row, current_chr);

        if ( now >= undo_last_set_time + 0.7f || Undo_Buffer.Count() == 0 ) {
            List<Undo_struct> ls = new List<Undo_struct>();
            ls.Add(u);
            Undo_Buffer.Add(ls);
            //Debug.Log("Undo add " + op.ToString() + " AS NEW (line: " + ind + ")");
        } else {
            List<Undo_struct> ls = Undo_Buffer[Undo_Buffer.Count() - 1];
            if (ls.Count > 0 && ls[ls.Count-1].line_ind == ind && ls[ls.Count-1].operation == undo_op.modify && op == undo_op.modify) {
                //If last operation was modify and current operation is modify - just skip
            } else {
                ls.Add(u);
                //Debug.Log("Undo add " + op.ToString() + " AS PREVIOUS (line: " + ind + ") txt = " + u.txt);
            }
            Undo_Buffer[Undo_Buffer.Count() - 1] = ls;
        }
        if (Undo_Buffer.Count > undo_limit) Undo_Buffer.RemoveAt(0);
        undo_last_set_time = now;

        if (OnTextChanged != null) OnTextChanged(this, new EventArgs());
    }

    void Undo_Restore() {
        if (Undo_Buffer.Count == 0) return;
        restoring_undo = true;

        Selection_Clear();

        List<Undo_struct> ls = Undo_Buffer[Undo_Buffer.Count() - 1];
        if (ls.Count > 0) { current_row = ls[0].cursor_pos.x; current_chr = ls[0].cursor_pos.y; }

        for (int i = ls.Count-1; i >= 0; i--) {
            Undo_struct cur = ls[i];
            if (cur.operation == undo_op.modify) {
                text_rows[cur.line_ind].text = "";
                text_char_w[cur.line_ind].Clear(); text_rows_w[cur.line_ind] = 0;
                current_row = cur.line_ind; current_chr = 0;
                type_text(cur.txt, true);

                Syntax_Highlight(cur.line_ind);
            } else if (cur.operation == undo_op.add) {
                Row_Erase(cur.line_ind);
            } else if (cur.operation == undo_op.remove) {
                Create_New_Row(cur.line_ind);
                current_row = cur.line_ind; current_chr = 0;
                type_text(cur.txt, true);
            }
        }

        Undo_Buffer.RemoveAt( Undo_Buffer.Count() - 1 );
        restoring_undo = false;
    }
    #endregion

    #region Code_highlight_and_Intellisense
    void Syntax_Highlight(int row) {
        if (!Syntax_Highlight_Enable) { 
            text_rows[row].gameObject.SetActive(true);
            colored_text[row].gameObject.SetActive(false);
            return;
        }

        colored_text[row].gameObject.SetActive(true);
        text_rows[row].gameObject.SetActive(false);

        //keywords
        Dictionary<Color, string[]> keywords_dict = new Dictionary<Color, string[]>();
        keywords_dict.Add(Color_Scheme.t_types, word_types);
        keywords_dict.Add(Color_Scheme.t_const, word_const);
        keywords_dict.Add(Color_Scheme.t_modif, word_modif);
        keywords_dict.Add(Color_Scheme.t_keyword, word_key);
        keywords_dict.Add(Color_Scheme.t_refl, word_refl);
        keywords_dict.Add(Color_Scheme.t_other, word_other);

        string txt = text_rows[row].text;
        string txt_new = "";
        int group_prev = -1;
        bool is_inside_color_tag = false;
        for (int i = 0; i < txt.Length; i++) {
            char c = txt[i];
            if (c == ' ') { txt_new += c; if (group_prev != 2) { group_prev = 3; } continue; }

            int group_cur = 0;
            if ( !charset_alphanumeric.Contains(c) ) group_cur = 1;

            //Quote
            if (group_prev == 2) group_cur = 2;
            if ( c == '"' && group_cur != 2) {
                if (is_inside_color_tag) txt_new += "</color>";
                txt_new += "<color=#" + ColorUtility.ToHtmlStringRGBA(Color_Scheme.t_quote) + ">"; is_inside_color_tag = true;
                group_cur = 2;
            }

            //Comment
            if (group_cur != 2 && i < txt.Length - 1 && c == '#') {
                if (is_inside_color_tag) txt_new += "</color>";
                txt_new += "<color=#" + ColorUtility.ToHtmlStringRGBA(Color_Scheme.t_comment) + ">" + txt.Substring(i);
                is_inside_color_tag = true; break;
            }

            //Keywords
            if (group_cur != 2 && group_prev != 0) {
                bool found_keyword = false;
                
                foreach (var kv in keywords_dict) {
                    foreach (string k in kv.Value) {
                        if (k.Length == 0) continue;

                        //If next character, after keyword, is a letter - don't consider as keyword
                        if (i < txt.Length - k.Length && charset_alphanumeric.Contains(txt.Substring(i + k.Length, 1)) ) continue;
                        
                        if (i < txt.Length - k.Length + 1 && txt.Substring(i, k.Length) == k) {
                            if (is_inside_color_tag) txt_new += "</color>";
                            txt_new += "<color=#" + ColorUtility.ToHtmlStringRGBA(kv.Key) + ">";
                            txt_new += txt.Substring(i, k.Length) + "</color>";
                            is_inside_color_tag = false; i += k.Length - 1; group_prev = -1; found_keyword = true; break;
                        }
                    }
                    if (found_keyword) break;
                }
                if (found_keyword) continue;
            }

            //Letters and Symbols
            if ( group_cur != group_prev && group_cur != 2 ) {
                if (is_inside_color_tag) { txt_new += "</color>"; is_inside_color_tag = false; }
                if (group_cur == 0) { txt_new += "<color=#" + ColorUtility.ToHtmlStringRGBA(Color_Scheme.t_std) + ">"; is_inside_color_tag = true; }
                if (group_cur == 1) { txt_new += "<color=#" + ColorUtility.ToHtmlStringRGBA(Color_Scheme.t_sym) + ">"; is_inside_color_tag = true; }
            }

            txt_new += c;

            //Quote closing
            if ( c == '"' && group_prev == 2) {
                group_cur = -1;
                txt_new += "</color>"; is_inside_color_tag = false;
            }

            group_prev = group_cur;
        }
        if (is_inside_color_tag) txt_new += "</color>";
        colored_text[row].text = txt_new;
    }

    void Intellisense_Init() {
        if (Intellisense_Settings.suggestion_item == null) return; //If there is no suggestion_item to show it's pretty much useless

        //Disable intellisense suggestion while loading class dictionary
        var sg_tmp = Intellisense_Settings.suggestion_item;
        Intellisense_Settings.suggestion_item = null;
        
        Event_Queue.Enqueue(On_Intellisense_BeginInit);
        bool READ_ASSEMBLIES_LIST_FROM_REFLECTION = Intellisense_Settings.get_assemblies_from_reflection;
		bool READ_ASSEMBLIES_LIST_FROM_FILE = Intellisense_Settings.get_assemblies_from_file;
        bool WRITE_ASSEMBLIES_LIST_TO_FILE = Intellisense_Settings.write_assemblies_to_file;
        bool WRITE_NAMESPACES_LIST_TO_FILE = Intellisense_Settings.write_namespaces_to_file;
        bool WRITE_TIMELOG = false;
		string assemblies_txt_file = "D:\\Unity 2018.1.0f2\\Projects\\Script-o-bot\\assemblies list.txt";
        string namespaces_txt_file = "D:\\Unity 2018.1.0f2\\Projects\\Script-o-bot\\assemblies namespaces.txt";
        string timelog_txt_file = "D:\\Unity 2018.1.0f2\\Projects\\Script-o-bot\\assemblies timelog.txt";

        //string [] exclude_assemblies_arr = new string[]{"UNITYEDITOR", "MCS", "UNITYENGINE.UI", "UNITYENGINE.COREMODULE", "NUNIT", "ASSEMBLY-CSHARP-", "UNITY.TEXTMESHPRO", "UNITY.CECIL", "UNITY.LEGACY"};
        string [] exclude_assemblies_arr = new string[]{"SYSTEM, ", "UNITYEDITOR", "MCS", "UNITYENGINE.UI", "NUNIT", "ASSEMBLY-CSHARP-", "UNITY.TEXTMESHPRO", "UNITY.CECIL", "UNITY.LEGACY"};
        //exclude_assemblies_arr = new string[]{};
        string[] exclude_namespaces_arr = exclude_namespaces.Split(new char[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);
        exclude_namespaces_arr = exclude_namespaces_arr.Select((s)=>s.ToUpper()).ToArray();
        //exclude_namespaces_arr = new string[]{};

        DateTime start_time = DateTime.Now;

		//test get all classes
        int total_entries = 0;
        DateTime current_time = DateTime.Now;
		if (!READ_ASSEMBLIES_LIST_FROM_FILE && READ_ASSEMBLIES_LIST_FROM_REFLECTION) {
			//Get classes from reflection
			System.IO.StreamWriter sw = null;
            System.IO.StreamWriter sw_N = null;
            System.IO.StreamWriter sw_T = null;
			if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw = System.IO.File.CreateText(assemblies_txt_file);
            if (WRITE_NAMESPACES_LIST_TO_FILE) sw_N = System.IO.File.CreateText(namespaces_txt_file);
            if (WRITE_TIMELOG) sw_T = System.IO.File.CreateText(timelog_txt_file);

            List<string> namespaces = new List<string>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(asm=> exclude_assemblies_arr.Where(a=> asm.FullName.ToUpper().StartsWith(a)).Count() == 0 );
            foreach (Assembly asm in assemblies)
			{
				//Debug.Log(asm.FullName);
				if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Assembly full name: " + asm.FullName);
				if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("----------------------------------------------------------------------------------");

                int t_count = 0;
                DateTime test_time = DateTime.Now;
                var namespaces_arr = asm.GetTypes().Select(x => x.Namespace).Distinct();
                if (WRITE_TIMELOG) sw_T.WriteLine("Got all namespaces of " + asm.FullName + " in " + (DateTime.Now - test_time).TotalSeconds + "sec");
                if (WRITE_NAMESPACES_LIST_TO_FILE) sw_N.WriteLine("Assembly: " + asm.FullName);
                foreach (var nmsp in namespaces_arr) {
                    string cur_namespace = nmsp;
                    if (cur_namespace == null) cur_namespace = "";

                    if (cur_namespace != "") {
                        var found = exclude_namespaces_arr.Where((s)=> cur_namespace.ToUpper().StartsWith(s) );
                        if (found.Count() > 0) continue;
                    }

                    if (!namespaces.Contains(cur_namespace)) {
                        namespaces.Add(cur_namespace);
                        if (WRITE_NAMESPACES_LIST_TO_FILE) {
                            var time_offset = (DateTime.Now - current_time).TotalSeconds;
                            sw_N.WriteLine("+" + time_offset.ToString("0.000") + "sec - " + cur_namespace);
                            current_time = DateTime.Now;
                        }
                    }

                    var types = asm.GetTypes().Where(x => x.Namespace == nmsp);
                    t_count += types.Count();
                    foreach (Type type in types)
                    {
                        if (type.IsNotPublic) continue;
                        total_entries++;

                        string t_name = type.FullName;
                        t_name = t_name.Replace("+", ".").Replace(">", "").Replace("<", "");
                        if (t_name.Contains("c__")) t_name = t_name.Substring(0, t_name.IndexOf("c__"));
                        if (t_name.Contains("`")) {
                            int ind1 = t_name.IndexOf("`");
                            int ind2 = t_name.IndexOf(".", ind1);
                            if (ind2 < 0)  t_name = t_name.Substring(0, ind1);
                            else           t_name = t_name.Substring(0, ind1) + t_name.Substring(ind2);
                        }

                        classes_dict current_level_dict = classes_dictionary;
                        foreach (string n in t_name.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!current_level_dict.dict.ContainsKey(n)) { current_level_dict.dict.Add(n, new classes_dict()); }
                            current_level_dict = current_level_dict.dict[n];
                        }
                        current_level_dict.full_name = type.FullName;

                        string ttt = "NotSet";
                        if (type.IsClass)     { ttt = "Class"; current_level_dict.type = classes_dict.Type_Enum.T_class; }
                        if (type.IsEnum)      { ttt = "Enum"; current_level_dict.type = classes_dict.Type_Enum.T_enum; }
                        if (type.IsInterface) { ttt = "Interface"; current_level_dict.type = classes_dict.Type_Enum.T_Interface; }
                        if (type.IsValueType && !type.IsEnum)  { ttt = "Struct"; current_level_dict.type = classes_dict.Type_Enum.T_struct; }
                        if (type.BaseType == typeof(MulticastDelegate)) { ttt = "Delegate"; current_level_dict.type = classes_dict.Type_Enum.T_Delegate; }

                        //string base_class = "";
                        string interfaces = ""; string generic_args = "";
                        if (type.IsClass) {
                            //Base class
                            //if (type.BaseType != null) base_class = " (Base class: " + type.BaseType.FullName + ")";
                            if (type.GetInterfaces().Length != 0) {
                                var i_str = type.GetInterfaces().Select(i=> i.ToString());
                                current_level_dict.derived_from.AddRange( i_str );
                                interfaces = " (Interfaces: " + string.Join( ", ", i_str) + ")";
                            }

                            //Class generic argument - Dictionary<TKey, TValue>();
                            Type[] arguments = type.GetGenericArguments();
                            if (arguments.Length > 0) {
                                if (WRITE_ASSEMBLIES_LIST_TO_FILE) generic_args = " (Generic arguments: " + string.Join(", ", arguments.Select(x => x.Name)) + ")";
                                foreach (var arg in arguments) {
                                    current_level_dict.T2.Add( new KeyValuePair<string, classes_dict>(arg.Name, null) );
                                }
                            }
                        }
                        if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Type: " + type.FullName + " (" + ttt + ")" + interfaces + generic_args );

                        if (type.IsEnum) {
                            foreach (string s in type.GetEnumNames()) {
                                if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Enum name: " + s);
                                if (!current_level_dict.dict.ContainsKey(s)) {
                                    var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.Enum_Value;
                                    current_level_dict.dict.Add(s, ncd);
                                }
                            }
                        }

                        ConstructorInfo[] methodInfos_constructor = type.GetConstructors();
                        foreach (var ci in methodInfos_constructor) {
                            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Constructor: " + ci.Name + " (param: " + string.Join(", ", ci.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
                        }

                        MethodInfo[] methodInfos = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        foreach (var mi in methodInfos) {
                            if (mi.IsSpecialName) continue;
                            total_entries++;
                            //if (mi.Name.ToLower() == "getcomponent") Debug.Log("Method: " + mi.Name + " (ret: " + mi.ReturnType.Name + ") (param: " + string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ") GenericMethod: " + mi.IsGenericMethod + " GenericDefinition: " + mi.IsGenericMethodDefinition);
                            //if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Method: " + mi.Name + " (ret: " + mi.ReturnType.Name + ") (param: " + string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");

                            if (WRITE_ASSEMBLIES_LIST_TO_FILE) {
                                string is_extension_str = "";
                                if (mi.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)) {
                                    is_extension_str = " (isExtension of " + mi.GetParameters()[0].ParameterType.ToString() + ")";
                                }
                                sw.WriteLine("Method: " + mi.Name + " (params: " + string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name + " " + p.ParameterType.IsGenericType)) + ") (ret: " + mi.ReturnType.Name + " (" + mi.ReturnType.FullName + "))" + is_extension_str ); // + " Attributes: " + mi.Attributes.ToString() );
                            } 

                            if (!current_level_dict.dict.ContainsKey(mi.Name)) {
                                var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.M_Method;
                                ncd.is_static = mi.IsStatic;
                                ncd.return_type = mi.ReturnType.Name; ncd.return_type_full = mi.ReturnType.FullName;
                                if (ncd.return_type_full == null && ncd.return_type != null) ncd.return_type_full = ncd.return_type;

                                var param = mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name);
                                ncd.method_signature = mi.ReturnType.Name + " " + mi.Name + " ( " + string.Join(", ", param) + " )";

                                // Get method generic arguments: GetComponent<T>();
                                Type[] g_arguments = mi.GetGenericArguments();
                                if (g_arguments.Length > 0) { // && (t_name.ToLower().EndsWith("list") || t_name.ToLower().EndsWith("dictionary"))) {
                                    //sw.WriteLine("- Generic arguments: " + string.Join(", ", g_arguments.Select(x => x.Name + " (" + x.FullName + ")")));
                                    foreach (var arg in g_arguments) {
                                        ncd.T2.Add( new KeyValuePair<string, classes_dict>(arg.Name, null) );
                                    }
                                }

                                current_level_dict.dict.Add(mi.Name, ncd);

                                //Extension methods handle
                                if (mi.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)) {
                                    //TODO Generic arguments
                                    //TODO Extensions of T or T[] - skipped for now
                                    string extension_of = mi.GetParameters()[0].ParameterType.ToString();
                                    if (extension_of == "T" || extension_of == "T[]") continue;
                                    int ind_of_backslash = extension_of.IndexOf("`");
                                    if (ind_of_backslash > 0) extension_of = extension_of.Substring(0, ind_of_backslash);

                                    classes_dict current_level_dict_for_ext = classes_dictionary;
                                    foreach (string n in  extension_of.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries)){
                                        if (!current_level_dict_for_ext.dict.ContainsKey(n)) { current_level_dict_for_ext.dict.Add(n, new classes_dict()); }
                                        current_level_dict_for_ext = current_level_dict_for_ext.dict[n];
                                    }
                                    if (!current_level_dict_for_ext.dict.ContainsKey(mi.Name)) {
                                        ncd.type = classes_dict.Type_Enum.M_ExtMethod;
                                        current_level_dict_for_ext.dict.Add(mi.Name, ncd);
                                    }
                                }
                            }
                        }

                        PropertyInfo[] propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        foreach (var pi in propertyInfos) {
                            total_entries++;
                            string indexer_params = "";
                            if (pi.GetIndexParameters().Length > 0) indexer_params = " (indexer params: " + string.Join(", ", pi.GetIndexParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")";
                            //if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Property: " + pi.Name + indexer_params + " (ret: " + pi.PropertyType.Name + ")");
                            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Property: " + pi.Name + indexer_params + " (ret: " + pi.PropertyType.Name + " (ret full: " + pi.PropertyType.FullName + "))");
                            if (!current_level_dict.dict.ContainsKey(pi.Name)) {
                                var ncd = new classes_dict();
                                ncd.type = (pi.GetIndexParameters().Length > 0) ? classes_dict.Type_Enum.P_Indexer : classes_dict.Type_Enum.P_Property;
                                ncd.is_static = pi.CanRead ? pi.GetGetMethod(true).IsStatic : false;
                                ncd.return_type = pi.PropertyType.Name; ncd.return_type_full = pi.PropertyType.FullName;
                                if (ncd.return_type_full == null && ncd.return_type != null) ncd.return_type_full = t_name + "." + ncd.return_type;
                                ncd.method_signature = pi.PropertyType.Name + " " + pi.Name;
                                current_level_dict.dict.Add(pi.Name, ncd);
                            }
                        }

                        if (type.FullName == "System.Array") {
                            var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.P_Indexer;
                            ncd.is_static = false;
                            ncd.return_type = "T"; ncd.return_type_full = "T";
                            ncd.method_signature = "T array_indexer";
                            current_level_dict.dict.Add("array_indexer", ncd);
                        }

                        FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        foreach (var fi in fieldInfos) {
                            total_entries++;
                            ttt = "NotSet";
                            var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.F_Field;
                            // IsLiteral determines if its value is written at compile time and not changeable
                            // IsInitOnly determines if the field can be set in the body of the constructor
                            // for C# a field which is readonly keyword would have both true but a const field would have only IsLiteral equal to true
                            if (fi.IsInitOnly) { ttt = "ReadOnly"; ncd.type = classes_dict.Type_Enum.F_READONLY; }
                            if (fi.IsLiteral && !fi.IsInitOnly) { ttt = "Const"; ncd.type = classes_dict.Type_Enum.F_CONST; }
                            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Field: " + fi.Name + " (" + ttt + ") (ret: " + fi.FieldType.Name + ")");
                            if (!current_level_dict.dict.ContainsKey(fi.Name)) { 
                                ncd.is_static = fi.IsStatic;
                                ncd.return_type = fi.FieldType.Name;
                                ncd.return_type_full = fi.FieldType.FullName;
                                ncd.method_signature = fi.FieldType.Name + " " + fi.Name;
                                current_level_dict.dict.Add(fi.Name, ncd);
                            }
                        }
                        EventInfo[] eventInfos = type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        foreach (var ei in eventInfos) {
                            total_entries++;
                            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Event: " + ei.Name);
                            if (!current_level_dict.dict.ContainsKey(ei.Name)) {
                                var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.E_Event;
                                ncd.is_static = ei.GetAddMethod().IsStatic;
                                current_level_dict.dict.Add(ei.Name, ncd);
                            }
                        }
                    }
                }
                if (WRITE_TIMELOG) { sw_T.WriteLine("Got all types of those namespaces in " + (DateTime.Now - test_time).TotalSeconds + "sec. Types count: " + t_count); sw_T.WriteLine(""); }
                if (WRITE_NAMESPACES_LIST_TO_FILE) sw_N.WriteLine("");
				if (WRITE_ASSEMBLIES_LIST_TO_FILE) { sw.WriteLine(""); sw.WriteLine(""); sw.WriteLine(""); }
			}

            //Inherited class
            Intellisense_Init_Derived_Recour( classes_dictionary.dict["System"].dict["Collections"] );

            //Namespaces
            namespaces.RemoveAt(0); //Remove empty namespace
            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("----------------------------------------------------------------------------------");
            foreach (string n in namespaces) {
                //Debug.Log("'" + n + "'");
                classes_dict current_level_dict = classes_dictionary;
                if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Namespace: " + n);
                foreach (string w in n.Split(new char[]{'.'})) {
                    if (current_level_dict.dict.ContainsKey(w)) {
                        current_level_dict.dict[w].type = classes_dict.Type_Enum.N_Namespace;
                        current_level_dict = current_level_dict.dict[w];
                    } else {
                        break;
                    }
                }
            }
            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.Close();
            if (WRITE_NAMESPACES_LIST_TO_FILE) sw_N.Close();
            if (WRITE_TIMELOG) sw_T.Close();
            Debug.Log ("Reflection init from reflection in " + (DateTime.Now - start_time).TotalSeconds + " seconds. Total entries: " + total_entries.ToString() );

		} else if (READ_ASSEMBLIES_LIST_FROM_FILE) {
			//Get classes from file
			var sr = System.IO.File.OpenText(assemblies_txt_file);
			classes_dict current_level_dict = classes_dictionary;

			while (!sr.EndOfStream) {
				string l = sr.ReadLine().Trim();

				if (l == "") continue;
				if (l.ToUpper().StartsWith("-")) continue;
				
				if (l.ToUpper().StartsWith("TYPE:")) {
					current_level_dict = classes_dictionary;

					string t_name = l.Substring(5).Trim();
					t_name = t_name.Replace("+", ".").Replace(">", "").Replace("<", "");
					if (t_name.Contains("`")) t_name = t_name.Substring(0, t_name.IndexOf("`"));
					if (t_name.Contains("c__")) t_name = t_name.Substring(0, t_name.IndexOf("c__"));

					foreach (string n in t_name.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries))
					{
						if (!current_level_dict.dict.ContainsKey(n)) current_level_dict.dict.Add(n, new classes_dict());
						current_level_dict = current_level_dict.dict[n];
					}
				}

				if (l.ToUpper().StartsWith("METHOD:")) {
					string m_name = l.Substring(7).Trim();
					if (!current_level_dict.dict.ContainsKey(m_name)) current_level_dict.dict.Add(m_name, new classes_dict());
				}
			}
            Debug.Log ("Reflection init from file in " + (DateTime.Now - start_time).TotalSeconds + " seconds.");
		} else {
            // for (int i = 0; i < 850000; i++) {
            //     var t = new classes_dict();
            //     t.method_signature = "asdfasdfad";
            //     t.return_type = "asdfasdfasdf";
            //     t.type = classes_dict.Type_Enum.F_READONLY;
            //     classes_dictionary.dict.Add(i.ToString(), t);
            // }

            // var x = new System.Xml.XmlDocument();
            // node_test = x.CreateElement("aaa");
            // for (int i = 0; i < 850000; i++) {
            //     var n = x.CreateElement("xxx");
            //     n.AppendChild( x.CreateElement("asdfasdfad") );
            //     n.AppendChild( x.CreateElement("es_dictionary.d") );
            //     n.AppendChild( x.CreateElement("t.type_classes_dict.Type_Enum.F_READONLY") );
            //     node_test.AppendChild(n);
            // }

            Debug.Log ("Reflection init from DUMMY in " + (DateTime.Now - start_time).TotalSeconds + " seconds.");
            //Debug.Log ("Reflection init from DUMMY in " + (DateTime.Now - start_time).TotalSeconds + " seconds. node_test.count " + node_test.ChildNodes.Count);
        }

        //Add built-in types
        if (classes_dictionary.dict.ContainsKey("System")) {
            var system = classes_dictionary.dict["System"];
            foreach (var kv in builtin_types) {
                string t = kv.Value.Substring(kv.Value.IndexOf(".") + 1);
                if (system.dict.ContainsKey(t)) classes_dictionary.dict.Add(kv.Key, system.dict[t]);
            }
        }

        Event_Queue.Enqueue(On_Intellisense_EndInit);
        Intellisense_Settings.suggestion_item = sg_tmp;
		// Type thisType = this.GetType();
		// MethodInfo theMethod = thisType.GetMethod("gameObject.transform.position.x");
		// theMethod.Invoke(this, null);
    }
    void Intellisense_Init_Derived_Recour(classes_dict cl) {
        foreach (var kv in cl.dict) {
            for (int d = 0; d < kv.Value.derived_from.Count; d++) {
                string d_mod = kv.Value.derived_from[d];
                if (!d_mod.Contains("]]")) { //TODO: Nested T i.e. - System.Collections.Generic.IEnumerable`1[System.Collections.Generic.KeyValuePair`2[TKey,TValue]]
                    kv.Value.derived_from[d] = "";
                    while (d_mod.Contains("[")) {
                        int ind1 = d_mod.LastIndexOf("[");
                        int ind2 = d_mod.LastIndexOf("]");
                        //Debug.Log(d_mod + " " + ind1 + " " + ind2);
                        kv.Value.derived_from[d] = d_mod.Substring(ind1 + 1, ind2 - ind1 - 1) + ";" + kv.Value.derived_from[d];
                        d_mod = d_mod.Remove(ind1, ind2 - ind1 + 1);
                    }
                }
                if (d_mod.Contains("`")) d_mod = d_mod.Substring(0, d_mod.IndexOf("`"));

                classes_dict current_level_dict = classes_dictionary;
                foreach (string n in d_mod.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!current_level_dict.dict.ContainsKey(n)) { current_level_dict = null; break; }
                    current_level_dict = current_level_dict.dict[n];
                }
                if (current_level_dict != null) kv.Value.derived_from_cl.Add(current_level_dict);
            }
            Intellisense_Init_Derived_Recour(kv.Value);
        }
    }
    void Intellisense_Init_Old() {
        if (Intellisense_Settings.suggestion_item == null) return; //If there is no suggestion_item to show it's pretty much useless

        //Disable intellisense suggestion while loading class dictionary
        var sg_tmp = Intellisense_Settings.suggestion_item;
        Intellisense_Settings.suggestion_item = null;
        
        Event_Queue.Enqueue(On_Intellisense_BeginInit);
        bool READ_ASSEMBLIES_LIST_FROM_REFLECTION = Intellisense_Settings.get_assemblies_from_reflection;
		bool READ_ASSEMBLIES_LIST_FROM_FILE = Intellisense_Settings.get_assemblies_from_file;
        bool WRITE_ASSEMBLIES_LIST_TO_FILE = Intellisense_Settings.write_assemblies_to_file;
        bool WRITE_NAMESPACES_LIST_TO_FILE = Intellisense_Settings.write_namespaces_to_file;
		string assemblies_txt_file = "D:\\Unity 2018.1.0f2\\Projects\\Script-o-bot\\assemblies list.txt";
        string namespaces_txt_file = "D:\\Unity 2018.1.0f2\\Projects\\Script-o-bot\\assemblies namespaces.txt";

        string [] exclude_assemblies_arr = new string[]{"UNITYEDITOR"};
        string[] exclude_namespaces_arr = exclude_namespaces.Split(new char[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);
        exclude_namespaces_arr = exclude_namespaces_arr.Select((s)=>s.ToUpper()).ToArray();

        DateTime start_time = DateTime.Now;

		//test get all classes
        DateTime current_time = DateTime.Now;
		if (!READ_ASSEMBLIES_LIST_FROM_FILE && READ_ASSEMBLIES_LIST_FROM_REFLECTION) {
			//Get classes from reflection
			System.IO.StreamWriter sw = null;
            System.IO.StreamWriter sw_N = null;
			if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw = System.IO.File.CreateText(assemblies_txt_file);
            if (WRITE_NAMESPACES_LIST_TO_FILE) sw_N = System.IO.File.CreateText(namespaces_txt_file);

            List<string> namespaces = new List<string>();

			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				//Debug.Log(asm.FullName);
				if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Assembly full name: " + asm.FullName);
				if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("----------------------------------------------------------------------------------");

				string t = asm.FullName.ToUpper();
				if (true || t.Contains("CSHARP") || t.Contains("MSCORLIB") || t.Contains("SYSTEM") || t.Contains("UNITYENGINE"))
				{
                    if (WRITE_NAMESPACES_LIST_TO_FILE) {
                        //TEST
                        DateTime test_time = DateTime.Now;
                        var namespaces_test = asm.GetTypes().Select(x => x.Namespace).Distinct();
                        sw_N.WriteLine("Got all namespaces of " + asm.FullName + " in " + (DateTime.Now - test_time).TotalSeconds + "sec");
                        
                        int t_count = 0;
                        test_time = DateTime.Now;
                        foreach (var nmsp in namespaces_test) {
                            //Debug.Log(nmsp);
                            if (!string.IsNullOrEmpty(nmsp)) {
                                var found = exclude_namespaces_arr.Where((s)=> nmsp.ToUpper().StartsWith(s) );
                                if (found.Count() > 0) continue;
                            }

                            //sw_N.WriteLine(nmsp);
                            var types = asm.GetTypes().Where(x => x.IsClass && x.Namespace == nmsp);
                            t_count += types.Count();
                        }
                        sw_N.WriteLine("Got all types of those namespaces in " + (DateTime.Now - test_time).TotalSeconds + "sec. Types count: " + t_count);
                    }

					foreach (Type type in asm.GetTypes())
					{
                        if (!string.IsNullOrEmpty(type.Namespace)) {
                            //if (exclude_namespaces_arr.Contains(type.Namespace.ToUpper())) continue;
                            var found = exclude_namespaces_arr.Where((s)=> type.Namespace.ToUpper().StartsWith(s) );
                            if (found.Count() > 0) continue;
                        } 

                        if (!namespaces.Contains(type.Namespace)) {
                            namespaces.Add(type.Namespace);
                            if (WRITE_NAMESPACES_LIST_TO_FILE) {
                                var time_offset = (DateTime.Now - current_time).TotalSeconds;
                                sw_N.WriteLine("+" + time_offset + "sec - " + type.Namespace);
                                current_time = DateTime.Now;
                            }
                        }

						//Debug.Log(type.FullName);
						//if (type.FullName.ToUpper().Contains("BOT")) Debug.Log(asm.FullName + " " + type.FullName);

						string t_name = type.FullName;
						t_name = t_name.Replace("+", ".").Replace(">", "").Replace("<", "");
						if (t_name.Contains("`")) t_name = t_name.Substring(0, t_name.IndexOf("`"));
						if (t_name.Contains("c__")) t_name = t_name.Substring(0, t_name.IndexOf("c__"));

						classes_dict current_level_dict = classes_dictionary;
						foreach (string n in t_name.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries))
						{
							if (!current_level_dict.dict.ContainsKey(n)) { current_level_dict.dict.Add(n, new classes_dict()); }
							current_level_dict = current_level_dict.dict[n];
						}

						string ttt = "NotSet";
                        if (type.IsClass) { ttt = "Class"; current_level_dict.type = classes_dict.Type_Enum.T_class; }
                        if (type.IsEnum)  { ttt = "Enum"; current_level_dict.type = classes_dict.Type_Enum.T_enum; }
                        if (type.IsValueType && !type.IsEnum)  { ttt = "Struct"; current_level_dict.type = classes_dict.Type_Enum.T_struct; }
                        if (type.BaseType == typeof(MulticastDelegate)) { ttt = "Delegate"; current_level_dict.type = classes_dict.Type_Enum.T_Delegate; }
						if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Type: " + type.FullName + " (" + ttt + ")");

                        if (type.IsClass) {
                            Type[] arguments = type.GetGenericArguments();
                            if (arguments.Length > 0) {
                                if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine ( "Generic arguments: " + string.Join(", ", arguments.Select(x => x.Name)) );
                            }
                        }

						if (type.IsEnum) {
							foreach (string s in type.GetEnumNames()) {
								if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Enum name: " + s);
                                if (!current_level_dict.dict.ContainsKey(s)) {
                                    var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.Enum_Value;
                                    current_level_dict.dict.Add(s, ncd);
                                }
							}
						}

                        ConstructorInfo[] methodInfos_constructor = type.GetConstructors();
                        foreach (var ci in methodInfos_constructor) {
                            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Constructor: " + ci.Name + " (param: " + string.Join(", ", ci.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
                        }

						MethodInfo[] methodInfos = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
						foreach (var mi in methodInfos) {
                            //if (mi.Name.ToLower() == "getcomponent") Debug.Log("Method: " + mi.Name + " (ret: " + mi.ReturnType.Name + ") (param: " + string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ") GenericMethod: " + mi.IsGenericMethod + " GenericDefinition: " + mi.IsGenericMethodDefinition);
                            // Get generic arguments: GetComponent<T>();
                            // Type[] arguments = mi.GetGenericArguments();
                            // if (arguments.Length > 0 && (t_name.ToLower().EndsWith("list") || t_name.ToLower().EndsWith("dictionary"))) {
                            //     Debug.Log("Method: " + mi.Name + " (ret: " + mi.ReturnType.Name + ") (param: " + string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ") GenericParams: " + string.Join(", ", arguments.Select(x => x.Name)));
                            // }
							//if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Method: " + mi.Name + " (ret: " + mi.ReturnType.Name + ") (param: " + string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
							if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Method: " + mi.Name + " (ret: " + mi.ReturnType.Name + ")");
							if (!current_level_dict.dict.ContainsKey(mi.Name)) {
                                var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.M_Method;
                                ncd.is_static = mi.IsStatic;
                                ncd.return_type = mi.ReturnType.Name; ncd.return_type_full = mi.ReturnType.FullName;
                                
                                var param = mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name);
                                ncd.method_signature = mi.ReturnType.Name + " " + mi.Name + " ( " + string.Join(", ", param) + " )";

                                current_level_dict.dict.Add(mi.Name, ncd);
                            }
						}

                        PropertyInfo[] propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        foreach (var pi in propertyInfos) {
                            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Property: " + pi.Name + " (ret: " + pi.PropertyType.Name + ")");
                            if (!current_level_dict.dict.ContainsKey(pi.Name)) {
                                var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.P_Property;
                                ncd.is_static = pi.CanRead ? pi.GetGetMethod(true).IsStatic : false;
                                ncd.return_type = pi.PropertyType.Name; ncd.return_type_full = pi.PropertyType.FullName;
                                ncd.method_signature = pi.PropertyType.Name + " " + pi.Name;
                                current_level_dict.dict.Add(pi.Name, ncd);
                            }
                        }

                        FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        foreach (var fi in fieldInfos) {
                            ttt = "NotSet";
                            var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.F_Field;
                            // IsLiteral determines if its value is written at compile time and not changeable
                            // IsInitOnly determines if the field can be set in the body of the constructor
                            // for C# a field which is readonly keyword would have both true but a const field would have only IsLiteral equal to true
                            if (fi.IsInitOnly) { ttt = "ReadOnly"; ncd.type = classes_dict.Type_Enum.F_READONLY; }
                            if (fi.IsLiteral && !fi.IsInitOnly) { ttt = "Const"; ncd.type = classes_dict.Type_Enum.F_CONST; }
                            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Field: " + fi.Name + " (" + ttt + ") (ret: " + fi.FieldType.Name + ")");
                            if (!current_level_dict.dict.ContainsKey(fi.Name)) { 
                                ncd.is_static = fi.IsStatic;
                                ncd.return_type = fi.FieldType.Name;
                                ncd.return_type_full = fi.FieldType.FullName;
                                ncd.method_signature = fi.FieldType.Name + " " + fi.Name;
                                current_level_dict.dict.Add(fi.Name, ncd);
                            }
                        }
                        EventInfo[] eventInfos = type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        foreach (var ei in eventInfos) {
							if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Event: " + ei.Name);
							if (!current_level_dict.dict.ContainsKey(ei.Name)) {
                                var ncd = new classes_dict(); ncd.type = classes_dict.Type_Enum.E_Event;
                                ncd.is_static = ei.GetAddMethod().IsStatic;
                                current_level_dict.dict.Add(ei.Name, ncd);
                            }
						}
					}
				}
				if (WRITE_ASSEMBLIES_LIST_TO_FILE) { sw.WriteLine(""); sw.WriteLine(""); sw.WriteLine(""); }
			}

            //Namespaces
            namespaces.RemoveAt(0); //Remove empty namespace
            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("----------------------------------------------------------------------------------");
            foreach (string n in namespaces) {
                //Debug.Log("'" + n + "'");
                classes_dict current_level_dict = classes_dictionary;
                if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.WriteLine("Namespace: " + n);
                foreach (string w in n.Split(new char[]{'.'})) {
                    if (current_level_dict.dict.ContainsKey(w)) {
                        current_level_dict.dict[w].type = classes_dict.Type_Enum.N_Namespace;
                        current_level_dict = current_level_dict.dict[w];
                    } else {
                        break;
                    }
                }
            }
            if (WRITE_ASSEMBLIES_LIST_TO_FILE) sw.Close();
            if (WRITE_NAMESPACES_LIST_TO_FILE) sw_N.Close();
            Debug.Log ("Reflection init from reflection in " + (DateTime.Now - start_time).TotalSeconds + " seconds.");

		} else if (READ_ASSEMBLIES_LIST_FROM_FILE) {
			//Get classes from file
			var sr = System.IO.File.OpenText(assemblies_txt_file);
			classes_dict current_level_dict = classes_dictionary;

			while (!sr.EndOfStream) {
				string l = sr.ReadLine().Trim();

				if (l == "") continue;
				if (l.ToUpper().StartsWith("-")) continue;
				
				if (l.ToUpper().StartsWith("TYPE:")) {
					current_level_dict = classes_dictionary;

					string t_name = l.Substring(5).Trim();
					t_name = t_name.Replace("+", ".").Replace(">", "").Replace("<", "");
					if (t_name.Contains("`")) t_name = t_name.Substring(0, t_name.IndexOf("`"));
					if (t_name.Contains("c__")) t_name = t_name.Substring(0, t_name.IndexOf("c__"));

					foreach (string n in t_name.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries))
					{
						if (!current_level_dict.dict.ContainsKey(n)) current_level_dict.dict.Add(n, new classes_dict());
						current_level_dict = current_level_dict.dict[n];
					}
				}

				if (l.ToUpper().StartsWith("METHOD:")) {
					string m_name = l.Substring(7).Trim();
					if (!current_level_dict.dict.ContainsKey(m_name)) current_level_dict.dict.Add(m_name, new classes_dict());
				}
			}
            Debug.Log ("Reflection init from file in " + (DateTime.Now - start_time).TotalSeconds + " seconds.");
		} else {
            // for (int i = 0; i < 850000; i++) {
            //     var t = new classes_dict();
            //     t.method_signature = "asdfasdfad";
            //     t.return_type = "asdfasdfasdf";
            //     t.type = classes_dict.Type_Enum.F_READONLY;
            //     classes_dictionary.dict.Add(i.ToString(), t);
            // }

            // var x = new System.Xml.XmlDocument();
            // node_test = x.CreateElement("aaa");
            // for (int i = 0; i < 850000; i++) {
            //     var n = x.CreateElement("xxx");
            //     n.AppendChild( x.CreateElement("asdfasdfad") );
            //     n.AppendChild( x.CreateElement("es_dictionary.d") );
            //     n.AppendChild( x.CreateElement("t.type_classes_dict.Type_Enum.F_READONLY") );
            //     node_test.AppendChild(n);
            // }

            Debug.Log ("Reflection init from DUMMY in " + (DateTime.Now - start_time).TotalSeconds + " seconds.");
            //Debug.Log ("Reflection init from DUMMY in " + (DateTime.Now - start_time).TotalSeconds + " seconds. node_test.count " + node_test.ChildNodes.Count);
        }

        //Add built-in types
        if (classes_dictionary.dict.ContainsKey("System")) {
            var system = classes_dictionary.dict["System"];
            foreach (var kv in builtin_types) {
                string t = kv.Value.Substring(kv.Value.IndexOf(".") + 1);
                if (system.dict.ContainsKey(t)) classes_dictionary.dict.Add(kv.Key, system.dict[t]);
            }
        }

        Event_Queue.Enqueue(On_Intellisense_EndInit);
        Intellisense_Settings.suggestion_item = sg_tmp;
		// Type thisType = this.GetType();
		// MethodInfo theMethod = thisType.GetMethod("gameObject.transform.position.x");
		// theMethod.Invoke(this, null);
	}
    void Intellisense_Init_Vars() {
        changed_rows = new ConcurrentQueue<changed_rows_info>();
        intellisense_vars = new classes_dict();
        intellisense_usings = new List<classes_dict>();
        vars_rows_assoc.Clear();
        vars_rows_assoc_rev.Clear();
        using_rows_assoc.Clear();
        using_rows_assoc_rev.Clear();
    }

    classes_dict Intellisense_AddUsing(string using_str, bool clear_old_using = false) {
        if (clear_old_using) intellisense_usings = new List<classes_dict>();
        string[] using_arr = using_str.Trim().Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries);

        classes_dict current_level_dict = classes_dictionary;
        for (int i = 0; i < using_arr.Length; i++) {
            if (current_level_dict.dict.ContainsKey(using_arr[i])) {
                current_level_dict = current_level_dict.dict[using_arr[i]];
            } else {
                //Not found
                return null;
            }
        }

        if (!intellisense_usings.Contains(current_level_dict)) intellisense_usings.Add(current_level_dict);
        return current_level_dict;
    }

//Extension methods not filtered by currently used namespaces
//int[][] a; a[0][0][0].--- should not suggest anything

//DONE: Multidimensional arrays not handled ( int[,] i )
//DONE: int.min/int.max are static values, why they are showing on instance member (variable)? -- it is a constant, and it was not checked for static correspondence
//DONE: Dictionary<int, string> qqq;                      -> fails, because of space - space split is used in Intellisense_var_check_bg()
//DONE: Dictionary<int, string> qqq; qqq[0].---           -> suggest TKey instead of TValue - string, instead of int
//DONE: Dictionary<int, string> qqq; qqq.get_Item().---   -> does not suggest anything
//DONE: List<string> sss; sss.get_Item().---              -> methods returning T does not working
//DONE: List<string> aaa; aaa.ToArray().---               -> does not suggest anything
//DONE: List<string> sss; sss.ToArray()[0].---            -> error
//DONE: qqq = Dictionary<int,string>(); qqq.---           -> this does not working at all
//DONE: Extension methods generics not handled, .ToArray()[0].--- -> not handled, List<string> lll; lll.ToArray()[0].--- suggest trash
//DONE: Dictionary<int, int> d; d.Values.ToArray()[0].--- -> not handled
//DONE: string[] s; int[] a; a[0].---, s[0].--- -> will suggest int, instead of string, because class_dict T is populated from last definition
//DONE: Arrays of arrays or arrays of enumerables ( List<int>[] l; l[0][0].--- ) -> not handled (Done in common_var, need to do in var_var)
//DONE: var x = new string[][3,456][]{}; x[1][2].--- suggest element instead of array

    void Intellisense(string text, int chr) {
        if (Intellisense_Settings.suggestion_item == null) {
            current_suggestion = new Vector2Int(-1 , -1); return;
        }

        char pressed_char = text[chr];
        if (pressed_char != '.' && !charset_alphanumeric.Contains(pressed_char)) { 
            //Hide intellisense and return
            Close_Intellisense();
            current_suggestion = new Vector2Int(-1 , -1); return;
        }

        //Get the part of the chain already typed
        int chain_begins = chr;
        int in_brackets = 0; int in_paranthesis = 0; int in_quotes = 0; int in_squotes = 0;
        //TODO: in <>, "", ''
        for (int i = chr; i >= 0; i--) {
            if (text[i] == '\"' && in_squotes == 0) { in_quotes = 1 - in_quotes; chain_begins = i; continue; }
            if (text[i] == '\'') { in_squotes = 1 - in_squotes; chain_begins = i; continue; }
            if (text[i] == ')') { in_paranthesis++; continue; }
            if (text[i] == '(') { in_paranthesis--; continue; }
            if (text[i] == ']') { in_brackets++; continue; }
            if (text[i] == '[') { in_brackets--; continue; }
            if (in_paranthesis > 0) continue;
            if (in_brackets > 0) continue;
            if (in_paranthesis < 0) { break; } //We found an opening paranthesis without closing contrepart, i.e. GetInput(K...
            if (in_brackets < 0) { break; } //We found an opening bracket without closing contrepart, i.e. GetInput[K...
            if (text[i] != '.' && !charset_alphanumeric.Contains(text[i])) break;
            chain_begins = i;
        }
        if (in_quotes != 0) { Close_Intellisense(); current_suggestion = new Vector2Int(-1 , -1); return; }
        
        int last_word_begin = chain_begins;
        int last_word_sym_count = chr - last_word_begin + 1;
        //Debug.Log("1: last_word_begin = " + last_word_begin + ", last_word_sym_count = " + last_word_sym_count);

        int paranthesis_length = 0;
        string words = text.Substring(last_word_begin, last_word_sym_count);
        foreach (Match m in paranthesis.Matches(words)) {
            words = words.Replace(m.Value, ""); paranthesis_length += m.Value.Length;
        }
        foreach (Match m in brackets_content.Matches(words)) {
            words = words.Replace(m.Value, "@"); paranthesis_length += m.Value.Length;
        }
        //Debug.Log("Suggestion: Found word: \"" + words + "\"");
        
        string[] words_arr = words.Trim().Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries);
        if (pressed_char == '.') words_arr = words_arr.Append("").ToArray();

        //var tms = DateTime.Now;
        string chain; bool chain_is_static;
        classes_dict current_level_dict = Intellisense_SearchChain(words_arr.Take(words_arr.Length-1).ToArray(), out chain, out chain_is_static);
        if (current_level_dict == null) {
            Close_Intellisense();
            current_suggestion = new Vector2Int(-1 , -1); return;
        }
        last_word_begin += chain.Length + paranthesis_length;
        last_word_sym_count -= (chain.Length + paranthesis_length);
        //Debug.Log("Suggestion found in " + (DateTime.Now-tms).TotalMilliseconds.ToString("N7") + "ms");
        //Debug.Log("Suggestion count " + current_level_dict.dict.Count);
        //Debug.Log("2: last_word_begin = " + last_word_begin + ", last_word_sym_count = " + last_word_sym_count + ", chain = " + chain);
        if (chain_begins > 0 && text.Substring(0, chain_begins).Trim().EndsWith("new")) chain_is_static = false;

        GameObject item_tmpl = Intellisense_Settings.suggestion_item;

        current_suggestion_signatures.Clear();
        for (int i = item_tmpl.transform.parent.childCount - 1; i >= 1; i--) {
            DestroyImmediate(item_tmpl.transform.parent.GetChild(i).gameObject);
        }

        int found_entries = 0;
        string last_word = words_arr[words_arr.Count()-1];

        int index_of_var_list = -1;
        List<classes_dict> search_list = new List<classes_dict>();
        search_list.Add(current_level_dict);
        if (current_level_dict.derived_from_cl.Count > 0) search_list.AddRange(current_level_dict.derived_from_cl);
        if (words_arr.Length == 1) { search_list.AddRange(intellisense_usings); search_list.Add(intellisense_vars); index_of_var_list = search_list.Count() - 1; }
        //Debug.Log("Inherited from: " + string.Join(", ", current_level_dict.derived_from_cl.Select(x=> x.full_name)) );

        //Debug.Log("Chain is static: " + chain_is_static);
        //classes_dict.Type_Enum[] check_for_static = new classes_dict.Type_Enum[]{ classes_dict.Type_Enum.T_struct, classes_dict.Type_Enum.M_Method, classes_dict.Type_Enum.P_Property, classes_dict.Type_Enum.F_Field, classes_dict.Type_Enum.F_CONST, classes_dict.Type_Enum.E_Event };
        classes_dict.Type_Enum[] check_for_static = new classes_dict.Type_Enum[]{ classes_dict.Type_Enum.M_Method, classes_dict.Type_Enum.P_Property, classes_dict.Type_Enum.F_Field, classes_dict.Type_Enum.F_CONST, classes_dict.Type_Enum.E_Event };

        int c = 0;
        foreach (classes_dict search_dict in search_list) {
            //Debug.Log("Search dict count: " + search_dict.dict.Count + ", Last word = '"  + last_word + "'");
            bool is_variable = (c == index_of_var_list); c++;

            foreach (var kv in search_dict.dict) {
                if (last_word == "" || kv.Key.StartsWith(last_word, StringComparison.OrdinalIgnoreCase)) {
                    //Debug.Log("Found: " + kv.Key + ", sig = " + kv.Value.method_signature + ", type = " + kv.Value.type.ToString() + ", static = " + kv.Value.is_static);

                    if (check_for_static.Contains(kv.Value.type) && chain_is_static != kv.Value.is_static) continue;
                    //Debug.Log("check static - pass");

                    //Check if the variable is available in the current context
                    if (is_variable && vars_rows_assoc.ContainsKey(kv.Key)) {
                        var q = vars_rows_assoc[kv.Key].Where( (vi)=> 
                            (vi.context[0] < current_row && vi.context[2] > current_row) ||
                            (vi.context[0] == current_row && vi.context[1] < current_chr) ||
                            (vi.context[2] == current_row && vi.context[3] > current_chr - 2)
                        ).Count();
                        if (q == 0) continue;
                    }
                    //Debug.Log("check context - pass");

                    found_entries++;

                    GameObject t = Instantiate(item_tmpl, item_tmpl.transform.parent);
                    t.GetComponentInChildren<Text>().text = kv.Key;
                    t.SetActive(true);

                    current_suggestion_signatures.Add( kv.Value.method_signature );

                    Image img = t.transform.GetChild(1).GetComponent<Image>();
                    switch (kv.Value.type) {
                        case classes_dict.Type_Enum.N_Namespace: img.sprite = Intellisense_Settings.icon_namespace; break;
                        case classes_dict.Type_Enum.T_Delegate:  img.sprite = Intellisense_Settings.icon_delegate; break;
                        case classes_dict.Type_Enum.T_class:     img.sprite = Intellisense_Settings.icon_class; break;
                        case classes_dict.Type_Enum.T_Interface: img.sprite = Intellisense_Settings.icon_interface; break;
                        case classes_dict.Type_Enum.T_struct:    img.sprite = Intellisense_Settings.icon_struct; break;
                        case classes_dict.Type_Enum.T_enum:      img.sprite = Intellisense_Settings.icon_enum; break;
                        case classes_dict.Type_Enum.Enum_Value:  img.sprite = Intellisense_Settings.icon_enum_value; break;
                        case classes_dict.Type_Enum.M_Method:    img.sprite = Intellisense_Settings.icon_method; break;
                        case classes_dict.Type_Enum.M_ExtMethod: img.sprite = Intellisense_Settings.icon_method_extension; break;
                        case classes_dict.Type_Enum.P_Property:  img.sprite = Intellisense_Settings.icon_property; break;
                        case classes_dict.Type_Enum.F_Field:     img.sprite = Intellisense_Settings.icon_field; break;
                        case classes_dict.Type_Enum.F_READONLY:  img.sprite = Intellisense_Settings.icon_field_readonly; break;
                        case classes_dict.Type_Enum.F_CONST:     img.sprite = Intellisense_Settings.icon_field_const; break;
                        default:                                 img.sprite = Intellisense_Settings.icon_not_set; break;
                    }
                    if (found_entries >= Intellisense_Settings.max_suggestion_count) break;
                }
            }
        }

        //Debug.Log("Found suggestion entries: " + found_entries + ", last_word_begin = " + last_word_begin + ", last_word_sym_count = " + last_word_sym_count);
        if (found_entries > 0) {
            suggestion_mouse_enabled = false;
            current_suggestion = new Vector2Int(last_word_begin, last_word_sym_count);
            if (Intellisense_Settings.suggestion_rt != null) Intellisense_Settings.suggestion_rt.gameObject.SetActive(true);
        }
        else {
            current_suggestion = new Vector2Int(-1 , -1);
            Close_Intellisense();
        }

        Intellisense_current_selection = 0;
        Intellisense_Navigation(nav_enum.Home); //Hide suggestion box if nothing to suggest and update current selection otherwise
    }
    void Intellisense_var_check_bg(System.Object o, DoWorkEventArgs args) {
        changed_rows_info rows_Info;
        while (true) {
            //Enable intellisense if needed
            if (Intellisense_Settings.get_assemblies_from_reflection && classes_dictionary.dict.Count == 0) {
                lock (classes_dictionary) { Intellisense_Init(); }
            }

            //Remove variables from deleted rows
            var keys = vars_rows_assoc_rev.Keys.ToArray();
            foreach (var k in keys) {
                if (k == null) Intellisense_var_check_bg_RemoveVarFromRow(k);
            }

            while ( changed_rows.TryDequeue(out rows_Info) ) {
                //Remove all variables and row association of current row
                if (vars_rows_assoc_rev.ContainsKey(rows_Info.associated_row)) {
                    Intellisense_var_check_bg_RemoveVarFromRow(rows_Info.associated_row);
                }
                if (using_rows_assoc_rev.ContainsKey(rows_Info.associated_row)) {
                    Intellisense_var_check_bg_RemoveUsingFromRow(rows_Info.associated_row);
                }

                //Remove comments
                int comment_ind = rows_Info.text.IndexOf("#");
                if (comment_ind == 0)       continue;
                else if (comment_ind > 0)   rows_Info.text = rows_Info.text.Substring(0, comment_ind);

                //Search for 'Type variable' declarations
                var matches = var_common.Matches(rows_Info.text);
                foreach (Match match_var_cmn in matches) {
                    //Debug.Log("Update variable declaration groups 1: " + match_var_cmn.Groups[1].Value + ", 2: " + match_var_cmn.Groups[2].Value + ", 3: " + match_var_cmn.Groups[3].Value + ", 4: " + match_var_cmn.Groups[4].Value + ", 5: " + match_var_cmn.Groups[5].Value + ", 6: " + match_var_cmn.Groups[6].Value);

                    var var_arr = match_var_cmn.Groups[3].Value.Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                    string chain = string.Join(" ", var_arr.Take(var_arr.Length - 1)); //Last word should be the variable name
                    //if (match_var_cmn.Groups[4].Success) var_arr[0] = var_arr[0].Replace(match_var_cmn.Groups[4].Value, ""); //Content in <>
                    //if (match_var_cmn.Groups[5].Success) chain = chain.Replace(match_var_cmn.Groups[5].Value, "") + "[]"; //Content in []
                    //Replace [] with content by just [] i.e. -> [,] by []
                    foreach (Match m in brackets.Matches(chain)) { chain = chain.Replace(m.Value, "[]"); }

                    string chain_out; bool tmp_chain_is_static;
                    string[] words_arr = chain.Trim().Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries);
                    classes_dict current_level_dict = Intellisense_SearchChain(words_arr, out chain_out, out tmp_chain_is_static, false);

                    //Debug.Log("Chain found " + string.Join(".", words_arr) + " = " + (current_level_dict != null) );
                    if (current_level_dict == null) continue;
                    //if (current_level_dict.T != null) Debug.Log("Declared array - OK");
                    //Debug.Log("Chain type " + current_level_dict.type );

                    bool valid_variable_type = false;
                    valid_variable_type = valid_variable_type || current_level_dict.type == classes_dict.Type_Enum.T_class;
                    valid_variable_type = valid_variable_type || current_level_dict.type == classes_dict.Type_Enum.T_enum;
                    valid_variable_type = valid_variable_type || current_level_dict.type == classes_dict.Type_Enum.T_struct;
                    valid_variable_type = valid_variable_type || current_level_dict.type == classes_dict.Type_Enum.T_Delegate;
                    if (!valid_variable_type) continue;

                    int[] context = new int[]{0, match_var_cmn.Index + match_var_cmn.Length, 0, 0};
                    Intellisense_VariableAddOrUpdate(current_level_dict, var_arr.Last(), rows_Info.associated_row, context);
                }

                //Search for 'var variable = Type' declarations
                //TODO: in "var t = BOT;" must not search for 'var'
                matches = var_var.Matches(rows_Info.text);
                foreach (Match match_var_cmn in matches) {
                    //Debug.Log("Update variable declaration groups VAR MODE 1: " + match_var_cmn.Groups[1].Value + ", 2: " + match_var_cmn.Groups[2].Value + ", 3: " + match_var_cmn.Groups[3].Value + ", 4: " + match_var_cmn.Groups[4].Value + ", 5: " + match_var_cmn.Groups[5].Value + ", 6: " + match_var_cmn.Groups[6].Value + ", 7: " + match_var_cmn.Groups[7].Value + ", 8: " + match_var_cmn.Groups[8].Value);
                    string var_name = match_var_cmn.Groups[1].Value.Trim();
                    string var_chain = match_var_cmn.Groups[3].Value.Trim();
                    if (var_chain.Replace("(", "").Length != var_chain.Replace(")", "").Length) continue; //If open/close paranthesis does not match
                    //if (match_var_cmn.Groups[7].Success) var_chain = var_chain.Replace(match_var_cmn.Groups[7].Value, "").Trim() + "[]"; //Content in []
                    //Replace [] with content by just [] i.e. -> [,] by []
                    foreach (Match m in brackets.Matches(var_chain)) { var_chain = var_chain.Replace(m.Value, "[]"); }
                    if (var_chain.Contains("{")) var_chain = var_chain.Substring(0, var_chain.IndexOf("{"));

                    foreach (Match m in paranthesis.Matches(var_chain)) var_chain = var_chain.Replace(m.Value, "");
                    string[] words_arr = var_chain.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries);

                    string tmp_chain; bool tmp_chain_is_static;
                    classes_dict current_level_dict = Intellisense_SearchChain(words_arr, out tmp_chain, out tmp_chain_is_static, true);

                    if (current_level_dict == null) continue;
                    //Debug.Log("Update variable declaration: " + var_name + " = " + tmp_chain + " (" + current_level_dict.type.ToString()+")");

                    int[] context = new int[]{0, match_var_cmn.Index + match_var_cmn.Length, 0, 0};
                    Intellisense_VariableAddOrUpdate(current_level_dict, var_name, rows_Info.associated_row, context);
                }

                //Check using;
                matches = using_regex.Matches(rows_Info.text);
                foreach (Match match_using in matches) {
                    //Debug.Log("detected 'using' " + match_using.Groups[1].Value);
                    Intellisense_UsingAddOrUpdate(match_using.Groups[1].Value, rows_Info.associated_row);
                }

                //Check context
                int cur_row = text_rows.IndexOf(rows_Info.associated_row);
                foreach (var kv in vars_rows_assoc) {
                    foreach (var vi in kv.Value) {
                        if (vi.context == null) continue;
                        int var_row = text_rows.IndexOf(vi.row);
                        if (var_row < 0) continue; //We are in baclground thread, and this row can be already deleted
                        int level = 0;
                        int[] end = new int[]{int.MaxValue, int.MaxValue};
                        for ( int r = var_row; r < text_rows.Count(); r++ ) {
                            var txt = text_rows[r].text;
                            for (int c = (r==var_row)?vi.context[1]:0 ; c < txt.Length; c++) {
                                if (txt[c] == '{') level++;
                                if (txt[c] == '}') level--;
                                if (level < 0) { end = new int[]{r, c}; goto exit_loop_label; }
                            }
                        }
                        exit_loop_label:
                        vi.context = new int[]{ var_row, vi.context[1], end[0], end[1] };
                        //Debug.Log("variable context: " + kv.Key + ": {" + var_row + ":" + vi.context[1] +"} {" + end[0] +":" + end[1] + "}");
                    }
                }
            }
            System.Threading.Thread.Sleep(300);
        }
    }
    void Intellisense_var_check_bg_RemoveVarFromRow(Text row) {
        string[] tmp; variable_info[] tmp2;

        //Iterate through all variables in this row
        foreach (string variable in vars_rows_assoc_rev[row]) {
            //Remove all associations with this row from the variable
            var var_arr = vars_rows_assoc[variable];
            vars_rows_assoc[variable] = var_arr.Where(vi=> vi.row != row).ToArray();
            //Debug.Log("vars_rows_assoc[variable].count without current row = " + vars_rows_assoc[variable].Count());
            if (vars_rows_assoc[variable].Count() == 0) {
                vars_rows_assoc.TryRemove(variable, out tmp2);
                intellisense_vars.dict.Remove(variable);
                //Debug.Log("Removing variable: " + variable);
            } else {
                intellisense_vars.dict[variable] = vars_rows_assoc[variable][vars_rows_assoc[variable].Count()-1].classes_dict;
            }
        }
        vars_rows_assoc_rev.TryRemove(row, out tmp);
    }
    void Intellisense_var_check_bg_RemoveUsingFromRow(Text row) {
        Text[] tmp3; classes_dict[] tmp4;
        //Iterate through all using in this row
        foreach (var using_cl in using_rows_assoc_rev[row]) {
            var using_arr = using_rows_assoc[using_cl];
            using_rows_assoc[using_cl] = using_arr.Where(t => t != row ).ToArray();
            if (using_rows_assoc[using_cl].Count() == 0) {
                //This using is removed
                using_rows_assoc.TryRemove(using_cl, out tmp3);
                intellisense_usings.Remove(using_cl);
                //Debug.Log("Removing using. Using count after removing: " + intellisense_usings.Count());
            } else {
                //This using is declared somewhere else, on another row
                //Debug.Log("Removing using. But using is used somewhere else. Using count still: " + intellisense_usings.Count());
            }
        }
        using_rows_assoc_rev.TryRemove(row, out tmp4);
    }
    void Intellisense_VariableAddOrUpdate(classes_dict variable_class_dict, string variable_name, Text row, int[] context = null) {
        //Add update variable declaration
        //Debug.Log("Update variable declaration: " + var_arr[1] + " = " + chain + " (" + current_level_dict.type.ToString()+")");
        var ncd = new classes_dict(); 
        ncd.type = variable_class_dict.type; ncd.dict = variable_class_dict.dict;
        ncd.full_name = variable_class_dict.full_name; ncd.T2 = variable_class_dict.T2;
        if (intellisense_vars.dict.ContainsKey(variable_name))
            intellisense_vars.dict[variable_name] = ncd;
        else 
            intellisense_vars.dict.Add(variable_name, ncd);

        if (vars_rows_assoc.ContainsKey(variable_name))
            vars_rows_assoc[variable_name] = vars_rows_assoc[variable_name].Append( new variable_info(row, variable_class_dict, context) ).ToArray();
        else
            vars_rows_assoc.TryAdd(variable_name, new variable_info[]{new variable_info(row, variable_class_dict, context)} );

        if (vars_rows_assoc_rev.ContainsKey(row))
            vars_rows_assoc_rev[row] = vars_rows_assoc_rev[row].Append(variable_name).ToArray();
        else
            vars_rows_assoc_rev.TryAdd(row, new string[]{variable_name});
    }
    void Intellisense_UsingAddOrUpdate(string using_name, Text row) {
        var cl = Intellisense_AddUsing(using_name);
        if (cl != null) {
            if (using_rows_assoc.ContainsKey(cl))
                using_rows_assoc[cl] = using_rows_assoc[cl].Append(row).ToArray();
            else
                using_rows_assoc.TryAdd(cl, new Text[]{row});

            if (using_rows_assoc_rev.ContainsKey(row))
                using_rows_assoc_rev[row] = using_rows_assoc_rev[row].Append(cl).ToArray();
            else
                using_rows_assoc_rev.TryAdd(row, new classes_dict[]{cl});
        }
        //Debug.Log("Using add " + using_name + ", using count now: " + intellisense_usings.Count());
    }
    classes_dict Intellisense_SearchChain(string[] words_arr, out string chain, out bool chain_is_static, bool search_in_vars = true, bool recur = false) {
        chain = "";
        chain_is_static = true;
        classes_dict current_level_dict = classes_dictionary;
        Stack<KeyValuePair<string, classes_dict>> current_word_generics = new Stack<KeyValuePair<string, classes_dict>>();

        //if (words_arr.Length > 0) Debug.Log("Searching chain: " + string.Join(".", words_arr) );
        for (int i = 0; i < words_arr.Length; i++) {
            //Debug.Log("Check for " + words_arr[i]);
            int is_array = 0;
            int is_array_element = 0;
            while (words_arr[i].EndsWith("[]")) { is_array++; words_arr[i] = words_arr[i].Substring(0, words_arr[i].Length - 2); }
            while (words_arr[i].EndsWith("@")) { is_array_element++; words_arr[i] = words_arr[i].Substring(0, words_arr[i].Length - 1); }
            //Debug.Log("Detected array level: " + is_array + ", array_element: " + is_array_element);

            //Generic definitions
            int ind1 = words_arr[i].IndexOf("<");
            int ind2 = words_arr[i].LastIndexOf(">");
            string[] generic_def = new string[]{};
            if (ind1 > 0 && ind2 > 0 && ind2 > ind1) {
                string generic = words_arr[i].Substring( ind1 + 1, ind2 - ind1 - 1 );
                generic_def = generic.Split(new char[]{','});
                words_arr[i] = words_arr[i].Remove( ind1, ind2 - ind1 + 1 );
                //Debug.Log("Generic def = '" + string.Join(",", generic_def) + "', new word = " + words_arr[i] );
            }

            //Search chain
            //TODO: Move all literal detections in "if (i == 0)" condition & make additional (i==1) condition for all decimals - i.e. 5.3f
            string chain_tmp; bool chain_tmp_is_static;
            if (words_arr[i] == "true" || words_arr[i] == "false") {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "Boolean"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].All(Char.IsDigit)) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "Int32"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].Length > 1 && (words_arr[i].EndsWith("f") || words_arr[i].EndsWith("F")) && words_arr[i].Take(words_arr[i].Length-1).All(c=> Char.IsDigit(c) || c == '.')) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "Single"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].Length > 1 && (words_arr[i].EndsWith("d") || words_arr[i].EndsWith("D")) && words_arr[i].Take(words_arr[i].Length-1).All(c=> Char.IsDigit(c) || c == '.')) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "Double"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].Length > 1 && (words_arr[i].EndsWith("l") || words_arr[i].EndsWith("L")) && words_arr[i].Take(words_arr[i].Length-1).All(c=> Char.IsDigit(c) || c == '.')) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "Int64"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].Length > 1 && (words_arr[i].EndsWith("m") || words_arr[i].EndsWith("M")) && words_arr[i].Take(words_arr[i].Length-1).All(c=> Char.IsDigit(c) || c == '.')) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "Decimal"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].Length > 1 && (words_arr[i].EndsWith("u") || words_arr[i].EndsWith("U")) && words_arr[i].Take(words_arr[i].Length-1).All(c=> Char.IsDigit(c) || c == '.')) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "UInt32"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].Length > 2 && (words_arr[i].EndsWith("ul") || words_arr[i].EndsWith("UL")) && words_arr[i].Take(words_arr[i].Length-2).All(c=> Char.IsDigit(c) || c == '.')) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "UInt64"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].StartsWith("\"") && words_arr[i].EndsWith("\"") ) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "String"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (words_arr[i].StartsWith("'") && words_arr[i].EndsWith("'") ) {
                current_level_dict = Intellisense_SearchChain(new string[]{"System", "SByte"}, out chain_tmp, out chain_tmp_is_static, false, true);
                chain_is_static = false; chain += words_arr[i] + ".";
            } else if (current_level_dict.dict.ContainsKey(words_arr[i])) {
                //Debug.Log("Found suggestion in main namespace.");
                current_level_dict = current_level_dict.dict[words_arr[i]];
                chain += words_arr[i] + ".";
            } else if (search_in_vars && i == 0 && intellisense_vars.dict.ContainsKey(words_arr[i])) {
                //Search in vars (first word only)
                //Debug.Log("Found in vars");
                chain_is_static = false;
                current_level_dict = intellisense_vars.dict[words_arr[i]];
                chain += words_arr[i] + ".";
                //if (is_array_element && current_level_dict.T == null) Debug.Log("Need variable array element, but T is null");
                for (int a = 0; a < is_array_element; a++) {
                    if (current_level_dict.T2.Count == 0) break;
                    //Debug.Log("Resolve array");
                    var indexer_property = current_level_dict.dict.Where(cl=> cl.Value.type == classes_dict.Type_Enum.P_Indexer).FirstOrDefault();
                    if (indexer_property.Key != null) {
                        var T = current_level_dict.T2.Where(kv => kv.Key == indexer_property.Value.return_type).FirstOrDefault();
                        if (T.Key != null) { 
                            current_level_dict = T.Value; 
                            //Debug.Log("Resolved array to: " + current_level_dict.full_name);
                        }
                    }
                }
            } else if (i == 0) {
                //If it's a first word - search also in usings
                bool found = false;
                foreach (var e in intellisense_usings) {
                    if (e.dict.ContainsKey(words_arr[i])) {
                        //Debug.Log("Found '" + words_arr[i] + "' in using namespace. Suggestions count: " + current_level_dict.dict.Count + ", word_arr = '" + string.Join("," , words_arr) + "'");
                        current_level_dict = e.dict[words_arr[i]];
                        chain += words_arr[i] + "."; found = true; break;
                    }
                }
                if (!found) return null;
            } else {
                //If it's not the first word - Try in base classes
                bool found = false;
                //foreach (var cl in current_level_dict.derived_from_cl) {
                for (int d = 0; d < current_level_dict.derived_from_cl.Count; d++) {
                    var cl = current_level_dict.derived_from_cl[d];
                    if (cl.dict.ContainsKey(words_arr[i])) {
                        //Set Generic definitions of the base class to the current class
                        foreach (string t in current_level_dict.derived_from[d].Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries) ) {
                            //var g = current_level_dict.T2.Where(kv => kv.Key == t);
                            var g = current_word_generics.Where(kv => kv.Key == t);
                            if (g.Count() == 1) { current_word_generics.Push( g.First() ); } //Debug.Log("Push generic def of the base class: " + g.First().Key + " -> " + g.First().Value.full_name); 
                        }

                        current_level_dict = cl.dict[words_arr[i]];
                        chain += words_arr[i] + "."; found = true; break;
                    }
                }
                if (!found) return null;
            }

            //Set generic types definition to T, i.e. --> Dictionary<string, int>
            for (int g = 0; g < generic_def.Length && g < current_level_dict.T2.Count(); g++ ) {
                if (recur) { Debug.Log("Prevent unexpected recursion when trying to get method: " + chain); return null; }

                generic_def[g] = generic_def[g].Trim();
                //Debug.Log("Searching for generic type: " + generic_def[g]);
                var generic_type = Intellisense_SearchChain(generic_def[g].Split(new char[]{'.'}), out chain_tmp, out chain_tmp_is_static, false, true);
                //Make copy to not alter original class_dict.T2
                current_level_dict = current_level_dict.Make_Copy();
                current_level_dict.T2[g] = new KeyValuePair<string, classes_dict>(current_level_dict.T2[g].Key, generic_type);
                //Debug.Log("Generic type: " + generic_def[g] + " found = " + (generic_type != null));
            }

            //Store generic definitions of current word (class or method) to use them in next words
            foreach (var kv in current_level_dict.T2) { 
                if(kv.Value != null) { current_word_generics.Push(kv); } //Debug.Log("Push generic def: " + kv.Key); }
            }

            //If we found a method, property, field (variable) or enum -- get it's return type
            //Debug.Log("Current type = " + current_level_dict.type.ToString());
            if (current_level_dict.type == classes_dict.Type_Enum.M_Method || current_level_dict.type == classes_dict.Type_Enum.M_ExtMethod || current_level_dict.type == classes_dict.Type_Enum.P_Property || current_level_dict.type == classes_dict.Type_Enum.F_Field) {
                if (recur) { Debug.Log("Prevent unexpected recursion when trying to get method: " + chain); return null; }

                //Debug.Log("Swap to the ret_type of the method '" + words_arr[i] + "': " + current_level_dict.return_type_full);
                chain_is_static = false;
                string return_type = current_level_dict.return_type_full.Trim();
                if (return_type == "System.Void" || return_type == "") return null;
                string[] words_arr2 = return_type.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries);
                var current_level_dict_new = Intellisense_SearchChain(words_arr2, out chain_tmp, out chain_tmp_is_static, false, true);

                if (current_level_dict_new == null) {
                    //If we didn't found the requested type, check if it is in generic attributes, i.e. --> List<string>().item
                    //Debug.Log("Return type '" + return_type + "' is probably generic.");
                    //Debug.Log("Generic definitions at current word: '" + string.Join(",", current_word_generics.Select(kv => kv.Key )) + "'" );
                    int make_arr = 0; //i.e. List<string>().ToArray()
                    while (return_type.EndsWith("[]")) { make_arr++; return_type = return_type.Substring(0, return_type.Length - 2); }
                    classes_dict generic = null; // = current_word_generics.Where(kv=> kv.Key == return_type).FirstOrDefault().Value;
                    if (current_level_dict.type == classes_dict.Type_Enum.M_ExtMethod) {
                        generic = current_word_generics.FirstOrDefault().Value; //TODO: This is wrong
                    } else {
                        generic = current_word_generics.Where(kv=> kv.Key == return_type).FirstOrDefault().Value;
                    }
                    //Debug.Log("Generic for current type found = " + (generic != null));
                    if (generic == null) return null;

                    current_level_dict = generic;
                    for (int a = 0; a < make_arr; a++) { current_level_dict = Intellisense_SearchChain_MakeArray(current_level_dict); }
                } else {
                    current_level_dict = current_level_dict_new;
                }
                
                //if (current_level_dict != null && is_array_element) current_level_dict = current_level_dict.T2[0].Value;
                for (int a = 0; a < is_array_element; a++) { current_level_dict = current_level_dict.T2[0].Value; }
                //Debug.Log("After swap, current_level_dict.count = " + current_level_dict.dict.Count.ToString() );
            }

            //Array
            //[] should be added to the chain in search_chain if they was removed. Ortherwise chain length is not calculated correctly when submit suggestion
            //Debug.Log("Detected array level: " + is_array);
            for (int a = 0; a < is_array; a++) { chain += "[]"; current_level_dict = Intellisense_SearchChain_MakeArray(current_level_dict); }

            //Debug.Log("chain is static: " + chain_is_static);
        }

        return current_level_dict;
    }
    classes_dict Intellisense_SearchChain_MakeArray(classes_dict class_dict)
    {
        //Debug.Log("Making array of " + class_dict.full_name);
        //chain += "[]";
        //chain_is_static = false; //???
        string chain2; bool chain_is_static2;
        var arr_class_dict = Intellisense_SearchChain(new string[]{"System", "Array"}, out chain2, out chain_is_static2, false, true);
        //We need make copy, otherwise - string[] s; int[] a; a[0].---, s[0].--- ->
        //      will suggest int, instead of string, because class_dict T is populated from last definition
        arr_class_dict = arr_class_dict.Make_Copy();
        var kv = new KeyValuePair<string, classes_dict>("T", class_dict);
        if (arr_class_dict.T2.Count == 0) arr_class_dict.T2.Add(kv); else arr_class_dict.T2[0] = kv;
        //Debug.Log("Generic definitions of array: '" + string.Join(",", current_level_dict.T2.Select(k => k.Key )) + "'" );
        return arr_class_dict;
    }

    int Intellisense_current_selection = 0;
    void Intellisense_Navigation(nav_enum nav, int select_item = -1)
    {
        //if (!Intellisense_Settings.suggestion_rt.gameObject.activeSelf) return;
        if (current_suggestion.x < 0 || current_suggestion.y < 0) {
            Close_Intellisense(); return;
        }

        Transform item_tmpl_root = Intellisense_Settings.suggestion_item.transform.parent;

        if (select_item >= 0) Intellisense_current_selection = select_item;

        if (nav == nav_enum.Submit) {
            //Submit suggestion
            if (Intellisense_current_selection >= item_tmpl_root.childCount - 1) return;

            string txt_row = text_rows[current_row].text;
            string txt = item_tmpl_root.GetChild(Intellisense_current_selection + 1).GetComponentInChildren<Text>().text;
            if ( current_suggestion_signatures[Intellisense_current_selection].EndsWith(")") ) txt += "()";

            int start = current_suggestion.x;
            int char_count = current_suggestion.y;
            text_rows[current_row].text = txt_row.Remove(start, char_count); //.Insert(start, txt); 
            text_rows_w[current_row] -= text_char_w[current_row].Skip(start).Take(char_count).Sum();
            text_char_w[current_row].RemoveRange(start, char_count);

            current_chr = start;
            type_text(txt);

            if (Intellisense_Settings.suggestion_rt != null) {
                Intellisense_Settings.suggestion_rt.gameObject.SetActive(false);
                if (select_item >= 0) mouse_is_over_my_child = false;
            }
            current_chr = start + txt.Length; Cursor_Update_Position();
            Syntax_Highlight(current_row);
            return;
        } 
        else if (nav == nav_enum.Up) {
            //Suggestion selector up
            Intellisense_current_selection--;
            Character_Repeat_Handler("SET", (KeyCode)10001);
        }
        else if (nav == nav_enum.Down) {
            //Suggestion selector down
            Intellisense_current_selection++;
            Character_Repeat_Handler("SET", (KeyCode)10002);
        }

        if (Intellisense_current_selection < 0) Intellisense_current_selection = 0;
        if (Intellisense_current_selection > Intellisense_Settings.suggestion_item.transform.parent.childCount - 2) Intellisense_current_selection = Intellisense_Settings.suggestion_item.transform.parent.childCount - 2;

        //If there is a signature - show it
        if (Intellisense_Settings.method_signature_text != null) {
            string sig = current_suggestion_signatures[Intellisense_current_selection];
            if ( string.IsNullOrEmpty(sig) ) {
                Intellisense_Settings.method_signature_text.enabled = false;
            } else {
                Intellisense_Settings.method_signature_text.text = sig;
                Intellisense_Settings.method_signature_text.enabled = true;
            }
        }

        //Selector - hide all and show current
        for (int i = 1; i < item_tmpl_root.childCount; i++) item_tmpl_root.GetChild(i).GetChild(0).gameObject.SetActive(false);
        item_tmpl_root.GetChild(Intellisense_current_selection + 1).GetChild(0).gameObject.SetActive(true);

        //Auto scroll to always show current selection
        if (select_item < 0) {
            float scroll_value = Mathf.InverseLerp(0f, (float)(Intellisense_Settings.suggestion_item.transform.parent.childCount - 2), (float)Intellisense_current_selection);
            //TODO: if there is no scroll - this will not scrolls at all
            if (Intellisense_Settings.suggestion_scroll != null) Intellisense_Settings.suggestion_scroll.value = 1f - scroll_value;
        }
    }
    #endregion

    #region  public_methods
    public string text {
        get {
            string txt = "";
            foreach ( Text t in text_rows ) {
                txt += t.text + "\n";
            }
            return txt;
        }
        set {
            //Wait for text_template
            // if (text_template == null) {
            //     set_text_in_next_frame(value); return;
            // }

            Selection_Clear();

            for (int i = text_rows.Count - 1; i >= 0; i--) {
                Destroy(text_rows[i].gameObject);
                text_rows.RemoveAt(i);
                text_char_w.RemoveAt(i);
                text_rows_w.RemoveAt(i);

                //Destroy highlighted text
                Destroy( colored_text[i].gameObject );
                colored_text.RemoveAt(i);
            }

            //Strip color tags if syntax highlight is enabled
            if (Syntax_Highlight_Enable) {
                var opt = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                value = System.Text.RegularExpressions.Regex.Replace(value, "<COLOR.*?>", "", opt);
                value = System.Text.RegularExpressions.Regex.Replace(value, "</COLOR>", "", opt);
            }
            Intellisense_Init_Vars();

            current_row = 0; current_chr = 0;
            Create_New_Row();
            string[] lines = value.Split("nl");

            foreach(string s in lines)
            {
                Debug.Log(s);
                type_text(s.Trim());
                SimulateEnterKey();
            }
            current_row = 0; current_chr = 0;
            Cursor_Update_Position();
            //if (ScrollH != null) { ScrollH.value = 0f; On_Scroll_H(0f); }
            //if (ScrollV != null) { ScrollV.value = 0f; On_Scroll_V(0f); }
        }
    }

    private void SimulateEnterKey()
    {
            //If enter is pressed in the middle of the row - split row and move right part to the next row
            string sel = Selection_Get_Text(true);
            if (current_chr < text_rows[current_row].text.Length)
            {
                Row_Split(current_row, current_chr);
                Syntax_Highlight(current_row);
                Syntax_Highlight(current_row + 1);
                if (sel == "")
                {
                    changed_rows.Enqueue(new changed_rows_info(text_rows[current_row], text_rows[current_row].text));
                    changed_rows.Enqueue(new changed_rows_info(text_rows[current_row + 1], text_rows[current_row + 1].text));
                }
            }
            else
            {
                Create_New_Row(current_row + 1);
            }
            current_row++; current_chr = 0;
            Cursor_Update_Position(); Selection_Clear(); Cursor_Blink(true); Update_Scroll_V_Size(); Update_Scroll_H_Size();
            Close_Intellisense();
            if (Input.GetKeyDown(KeyCode.Return)) Character_Repeat_Handler("SET", KeyCode.Return); else Character_Repeat_Handler("SET", KeyCode.KeypadEnter);
        
    }

    public bool enable_intellisense {
        get { 
            return Intellisense_Settings.get_assemblies_from_reflection; 
        }
        set { 
            if (value && !Intellisense_Settings.get_assemblies_from_reflection) {
                Intellisense_Settings.get_assemblies_from_reflection = true;
                Intellisense_AddUsing("UnityEngine");
            }
            if (!value && Intellisense_Settings.get_assemblies_from_reflection) {
                Intellisense_Settings.get_assemblies_from_reflection = false;
                classes_dictionary.dict.Clear();
                classes_dictionary = new classes_dict();
                Intellisense_Init_Vars();
            }
        }
    }

    public float line_height {
        get { return lineHeight; }
    }

    public float line_offset (float n) {
        Vector2 offset = new Vector2(0f, (n * lineHeight) + (lineHeight * 0.5f) );
        Rect screenR = txt_cnt_rt.ScreenSpaceBounds(offset);
        return screenR.y;
    }

    public Vector2 line_position (int n) {
        if (n >= text_rows.Count() ) n = text_rows.Count() - 1;
        if (n < 0) return Vector2.zero;
        
        //Vector3 pos_3d = text_rows[n].transform.position;
        //return new Vector2(pos_3d.x, pos_3d.y );
        Rect b = text_rows[n].GetComponent<RectTransform>().ScreenSpaceBounds();
        return new Vector2( b.xMin, b.yMax );
    }

    public void Activate() {
        Activate_Settings.IsActive = true;
        if (Activate_Settings.Activation_Border != null) Activate_Settings.Activation_Border.SetActive(true);
        Cursor_Blink(true);
    }

    public void DeActivate() {
        Activate_Settings.IsActive = false;
        ready_to_select = false; //If we was in process of selecting
        if (Activate_Settings.Activation_Border != null) Activate_Settings.Activation_Border.SetActive(false);
    }
    
    public void UpdateCursor() {
        Cursor_Update_Position();
    }

    public bool Close_Intellisense() {
        var rt = Intellisense_Settings.suggestion_rt;
        if (Intellisense_Settings.suggestion_rt != null && rt.gameObject.activeSelf) {
            Intellisense_Settings.suggestion_rt.gameObject.SetActive(false);
            return true;
        } else {
            return false;
        }
    }

    public int Get_Line_By_Char_Index(int char_index) {
        int cur_row_start_char = 0;
        for (int r = 0; r < text_char_w.Count; r++) {
            int row_char_count = text_char_w[r].Count + 1;
            if (cur_row_start_char + row_char_count > char_index) return r;
            cur_row_start_char += row_char_count;
        }
        return -1;
    }

    public bool Have_breakpoint(int line) {
        if (line >= text_rows.Count) return false;
        return breakpoints.ContainsKey( text_rows[line] );
    }
    #endregion


    void Character_Repeat_Handler(string operation, KeyCode k = KeyCode.None) {
        bool need_reset = false;
        if (operation == "SET") {
            if ((int)k < 1000) {
                if (Input.GetKeyDown(k)) need_reset = true;
            } else if ((int)k == 10001) {
                if (Engine.check_key(Engine.Key.Intellisense_Up)) need_reset = true;
            } else if ((int)k == 10002) {
                if (Engine.check_key(Engine.Key.Intellisense_Down)) need_reset = true;
            }
        }

        if (need_reset) {
            repeat_start_timer = 0f;
            repeat_key = k;
            repeat_ratio_timer = 100000f;
        }
    }

    int[] Char_Index_Under_Mouse() {
        Vector2 coord = Vector2.zero;
        Camera cam = GetComponentInParent<Canvas>().worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(txt_cnt_rt, Input.mousePosition, cam, out coord);

        //Pivot offset
        coord = coord + (txt_cnt_rt.rect.size / 2);
        coord.y = txt_cnt_rt.rect.height - coord.y;

        bool withing =       coord.x > -txt_cnt_rt.anchoredPosition.x && coord.y > 0;
        withing = withing && coord.x < -txt_cnt_rt.anchoredPosition.x + txt_cnt_rt.rect.width;
        withing = withing && coord.y < txt_cnt_rt.anchoredPosition.y + txt_cnt_rt.rect.height;

        if (!withing) return new int[]{-1, -1};

        coord.y = coord.y - (lineHeight / 2); //better, more intuitive
        int row = Mathf.RoundToInt( coord.y / lineHeight);
        if (row > text_rows.Count-1) row = text_rows.Count-1;

        float x = 0;
        int col = 0;
        for (int c = 0; c < text_char_w[row].Count; c++) {
            col = c;
            x += text_char_w[row][c];
            if (x > coord.x) break;

            //If last character, and x still greater - move to last position
            if (c == text_char_w[row].Count - 1) col++;
        }
        if (col > text_rows[row].text.Length) col = text_rows[row].text.Length;   

        //Debug.Log ("Click at row: " + row + ", col: " + col);
        return new int[]{row, col};
    }

    public void Color_Scheme_Apply(bool update_syntax_highlight = false) {
        //Background
        GetComponent<Image>().color = Color_Scheme.background;

        //Cursor
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color_Scheme.cursor); tex.Apply();
        cursor.GetComponent<RawImage>().texture = tex;

        //Selector
        Texture2D tex2 = new Texture2D(1, 1);
        tex2.SetPixel(0, 0, Color_Scheme.selection_color); tex2.Apply();
        selector.GetComponent<RawImage>().texture = tex2;

        //scroll bars
        List<Scrollbar> ls = new List<Scrollbar>();
        if (ScrollH != null) ls.Add (ScrollH);
        if (ScrollV != null) ls.Add (ScrollV);
        foreach (Scrollbar scrl in ls) {
            var sc = scrl.colors; 
            sc.normalColor = Color_Scheme.scrollbar_front;
            sc.highlightedColor = Color_Scheme.scrollbar_front_highlight;
            sc.pressedColor = Color_Scheme.scrollbar_front_pressed;
            scrl.colors = sc;

            scrl.GetComponent<Image>().color = Color_Scheme.scrollbar_back;
        }

        //Text default
        text_template.GetComponent<Text>().color = Color_Scheme.t_std;
        foreach (Text t in text_rows) t.color = Color_Scheme.t_std;

        if (update_syntax_highlight) {
            for (int i = 0; i < text_rows.Count(); i++) Syntax_Highlight(i);
        }
    }

    public void Update_Text_Size() {
        Text t = text_template.GetComponent<Text>();
        t.fontSize = font_size;

        Canvas cnv = GetComponentInParent<Canvas>();
        var extents = t.cachedTextGenerator.rectExtents.size * 0.5f;
        lineHeight = t.cachedTextGeneratorForLayout.GetPreferredHeight("A", t.GetGenerationSettings(extents)) / cnv.scaleFactor;
        cursor.GetComponent<RectTransform>().sizeDelta = new Vector2(cursor_width, lineHeight);

        for (int r = 0; r < text_rows.Count; r++) {
            text_rows[r].fontSize = font_size;
            colored_text[r].fontSize = font_size;
            
            text_rows[r].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -r * lineHeight);
            colored_text[r].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -r * lineHeight);
        }
        Recalculate_text_width();
        Selection_Draw(selection_start, selection_end); Update_Scroll_V_Size(); Update_Scroll_H_Size();
    }

    public void Recalculate_text_width() {
        //Recalculate text width
        canvas_scale = GetComponentInParent<Canvas>().transform.localScale.x;
        scaled_font_size = Mathf.FloorToInt(font_size * canvas_scale);

        //Debug.Log("Recalculate width: canvas_scale: " + canvas_scale +", scaled_font_size: " + scaled_font_size);

        for (int r = 0; r < text_rows.Count; r++) {
            text_char_w[r].Clear();
            text_rows_w[r] = 0f;
            text_rows[r].font.RequestCharactersInTexture(text_rows[r].text, scaled_font_size, text_rows[r].fontStyle);
            foreach(char chr in text_rows[r].text) {
                CharacterInfo ci = new CharacterInfo();

                text_rows[r].font.GetCharacterInfo(chr, out ci, scaled_font_size, text_rows[r].fontStyle );
                float advance = ci.advance / canvas_scale;
                text_char_w[r].Add(advance);
                text_rows_w[r] += advance;
            }
        }

        Cursor_Update_Position();
    }

    public void HighlightText(int start_char, int end_char, Color? color = null){
        //Debug.Log("Highlight start: " + start_char + " end: " + end_char);
        if (start_char < 0) {
            //Reset highlight
            // for (int i = selector_highlighters.Count - 1; i >= 0; i-- ) Destroy( selector_highlighters[i] );
            // selector_highlighters.Clear();
            // return;
            HighlightText(new int[]{-1, -1}, new int[]{-1, -1}); return;
        }

        // Color c = color ?? Color_Scheme.highlight_default_color;
        // Texture2D tex = new Texture2D(1, 1);
        // tex.SetPixel(0, 0, c ); tex.Apply();

        var h_start = new int[]{-1, -1};
        var h_end = new int[]{-1, -1};

        int cur_row_start_char = 0;
        for (int r = 0; r < text_char_w.Count; r++) {
            int row_char_count = text_char_w[r].Count + 1;
            
            if (h_start[0] == -1 && cur_row_start_char <= start_char && cur_row_start_char + row_char_count > start_char) 
                { h_start = new int[]{r, start_char - cur_row_start_char}; }

            if (h_end[0] == -1 && cur_row_start_char <= end_char && cur_row_start_char + row_char_count > end_char)
                { h_end = new int[]{r, end_char - cur_row_start_char}; break; }

            cur_row_start_char += row_char_count;
        }

        // if (h_start[0] != -1 && h_end[0] != -1) {
        //     selection_start = h_start;
        //     selection_end = h_end;
        //     //Debug.Log("Highlight, draw selection, start: " + selection_start[0] + "x" + selection_start[1] + ", end: " + selection_end[0] + "x" + selection_end[1]);
        //     Selection_Draw(selection_start, selection_end);
        // }

        // foreach (var kv in selector_per_row) { 
        //     kv.Value.GetComponent<RawImage>().texture = tex;
        //     selector_highlighters.Add(kv.Value.gameObject); 
        // }
        // selector_per_row.Clear();

        HighlightText(h_start, h_end, color);
    }
    public void HighlightText(int[] start_row_char, int[] end_row_char, Color? color = null){
        if (start_row_char[0] < 0 || start_row_char[1] < 0) {
            //Reset highlight
            for (int i = selector_highlighters.Count - 1; i >= 0; i-- ) Destroy( selector_highlighters[i] );
            selector_highlighters.Clear();
            return; 
        }

        Color c = color ?? Color_Scheme.highlight_default_color;
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, c ); tex.Apply();

        if (start_row_char[0] != -1 && end_row_char[0] != -1) {
            selection_start = start_row_char;
            selection_end = end_row_char;
            //Debug.Log("Highlight, draw selection, start: " + selection_start[0] + "x" + selection_start[1] + ", end: " + selection_end[0] + "x" + selection_end[1]);
            Selection_Draw(selection_start, selection_end);
        }

        foreach (var kv in selector_per_row) { 
            kv.Value.GetComponent<RawImage>().texture = tex;
            selector_highlighters.Add(kv.Value.gameObject);
        }
        selector_per_row.Clear();
    }
    public void SetBreakpoint() {
        if (breakpoints.ContainsKey(text_rows[current_row])) {
            Destroy(breakpoints[text_rows[current_row]]);
            breakpoints.Remove(text_rows[current_row]);
        } else {
            int length = text_rows[current_row].text.Length;
            if (length > 0) {
                HighlightText(new int[]{current_row, 0}, new int[]{current_row, length}, Color_Scheme.highlight_breakpoint_color);
                GameObject breakpoint_obj = selector_highlighters[selector_highlighters.Count-1];
                breakpoints.Add(text_rows[current_row], breakpoint_obj);
                selector_highlighters.RemoveAt(selector_highlighters.Count-1);

                breakpoint_obj.transform.SetParent(transform, true);
                breakpoint_obj.transform.SetSiblingIndex(0);
                var breakpoint_obj_rt = breakpoint_obj.GetComponent<RectTransform>();
                breakpoint_obj_rt.sizeDelta = new Vector2(txt_cnt_rt.rect.width, breakpoint_obj_rt.sizeDelta.y);

                float offset_Y = breakpoint_obj.transform.position.y - text_rows[current_row].transform.position.y;
                var pc = breakpoint_obj.AddComponent<UnityEngine.Animations.ParentConstraint>();
                pc.rotationAxis = UnityEngine.Animations.Axis.None; pc.translationAxis = UnityEngine.Animations.Axis.Y;
                pc.translationOffsets = new Vector3[]{ new Vector3(0f, offset_Y, 0f) };

                var src = new UnityEngine.Animations.ConstraintSource();
                src.sourceTransform = text_rows[current_row].transform; src.weight = 1f;
                pc.AddSource(src); pc.constraintActive = true;
            }
        }
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) {
        mouse_is_over_me = true;
        if (!Activate_Settings.IsActive) {
            if (Activate_Settings.Activate == Activate_Scheme_Info.Activate_Enum.OnMouseOver) Activate();
        }
    }
    public void OnPointerEnterChild(UnityEngine.EventSystems.PointerEventData eventData) {
        mouse_is_over_my_child = true;
        OnPointerEnter(eventData);
    }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) {
        mouse_is_over_me = false;
        if (Activate_Settings.IsActive) {
            if (Activate_Settings.DeActivate == Activate_Scheme_Info.DeActivate_Enum.OnMouseOut) DeActivate();
        }

        //List of ALL ui objects under cursor
        // PointerEventData pointerData = new PointerEventData (EventSystem.current) { pointerId = -1 };
        // pointerData.position = Input.mousePosition;
        // List<RaycastResult> results = new List<RaycastResult>();
        // EventSystem.current.RaycastAll(pointerData, results);
        // foreach (var p in results) Debug.Log( p.gameObject.name );
    }
    public void OnPointerExitChild(UnityEngine.EventSystems.PointerEventData eventData) {
        mouse_is_over_my_child = false;
        OnPointerExit(eventData);
    }
    public void OnPointerClickSuggestionItem(int n) { Intellisense_Navigation(nav_enum.Submit, n); }
    public void OnPointerEnterSuggestionItem(int n) { if (suggestion_mouse_enabled) Intellisense_Navigation(nav_enum.Mouse_Scroll, n); }
}

public class Text_Editor_Child_Element : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public Text_Editor te = null;
    bool mouse_is_on_me = false;
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) {
        //Debug.Log("Mouse Enter: " + gameObject.name);
        mouse_is_on_me = true;
        if (te != null) te.OnPointerEnterChild(eventData);
    }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) {
        //Debug.Log("Mouse Leave: " + gameObject.name);
        mouse_is_on_me = false;
        if (te != null) te.OnPointerExitChild(eventData);
    }
    void OnDisable() {
        if (te != null && mouse_is_on_me) { 
            //Debug.Log("Mouse Leave - Disabled: " + gameObject.name); 
            te.OnPointerExitChild(null); 
        }
    }
}
public class Text_Editor_Suggestion_Item : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler {
    public Text_Editor te = null;
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) {
        if (te != null) te.OnPointerEnterSuggestionItem( transform.GetSiblingIndex() - 1 );
    }
    public void OnPointerClick(PointerEventData p)
    {
        if (te != null) te.OnPointerClickSuggestionItem( transform.GetSiblingIndex() - 1 );
    }
}

#region extensions
namespace Rect_Extension
{
    public static class Extensions {
        public static Rect ScreenSpaceBounds( this RectTransform rt, Vector2 offset = new Vector2() ) {
            Vector2 size = Vector2.Scale(rt.rect.size, rt.lossyScale);
            //Rect rect = new Rect(rt.position.x + (offset.x * rt.lossyScale.x), Screen.height - (rt.position.y - (offset.y * rt.lossyScale.y)), size.x, size.y);
            //rect.x -= (rt.pivot.x * size.x);
            //rect.y -= ((1.0f - rt.pivot.y) * size.y);

            Rect rect = new Rect(rt.position.x, rt.position.y, size.x, size.y);
            rect.x -= (rt.pivot.x * size.x);
            rect.y += ((1.0f - rt.pivot.y) * size.y);
            rect.x += offset.x * rt.lossyScale.x;
            rect.y -= offset.y * rt.lossyScale.y;

            rect.y = Screen.height - rect.y;

            //Debug.Log("size.y: " + rt.rect.size.y + ", scale.y: " + rt.lossyScale.y + ", position.y: " + rt.position.y + ", screen_height: " + Screen.height + ", offset.y: " + offset.y );
            //Debug.Log("Center_y: " + rt.position.y + ", ");
            //Debug.Log("return_y: " + rect.y);

            return rect;
        }

        public static Vector3 CanvasSpaceToWorld (this Canvas cv, Vector2 pos) {
            RectTransform cv_rect = cv.GetComponent<RectTransform>();
            return new Vector3(pos.x * cv.scaleFactor, (cv_rect.rect.height * cv.scaleFactor) - pos.y, cv_rect.position.z );
        }
    
        public static T test_ext_of_t<T>(this List<T> l) {
            return (T) Convert.ChangeType(null, typeof(T));
        }
    }
}
namespace System.Collections.Concurrent
{
    public class ConcurrentDictionary_Ordered<TKey,TValue>
    {
        List<TKey> orders = new List<TKey>();
        ConcurrentDictionary<TKey, TValue> dict = new ConcurrentDictionary<TKey, TValue>();

        public void Add(TKey key, TValue value) {
            dict.TryAdd(key, value);
            orders.Add(key);
        }

        //Indexers
        public TValue this[TKey key] {
            get { return dict[key]; }
            set { dict[key] = value; }
        }
        public TValue this[int index] {
            get { return dict[orders[index]]; }
            set { dict[orders[index]] = value; }
        }

    }
}
#endregion

public static class Engine{
	public static Dictionary<Key, KeyCode[][]> Hotkeys = new Dictionary<Key, KeyCode[][]>();
	public static Dictionary<Key, KeyCode[][]> Hotkeys_Defaults = new Dictionary<Key, KeyCode[][]>();
	public static Dictionary<Key, string> Hotkeys_descriptions = new Dictionary<Key, string>();
	public enum Key
	{
		Set_Breakpoint,
		Intellisense_Up, Intellisense_Down, Intellisense_Submit, Intellisense_Enable,
        Debug_Force_Update
	}

	public static bool check_key(Key k, bool down = true)
	{
		foreach (KeyCode[] key_arr in Hotkeys[k]) {
			bool ok = true;
			foreach (KeyCode kc in key_arr) {
				if (kc == KeyCode.LeftAlt || kc == KeyCode.RightAlt)
					if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) continue; else { ok = false; break; }
				if (kc == KeyCode.LeftShift || kc == KeyCode.RightShift)
					if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) continue; else { ok = false; break; }
				if (kc == KeyCode.LeftControl || kc == KeyCode.RightControl)
					if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) continue; else { ok = false; break; }

				if (!down && !Input.GetKey(kc)) { ok = false; break; }
				else if (down && !Input.GetKeyDown(kc)) { ok = false; break; }
			}

			if (ok) return true;
		}

		// string str = "";
		// foreach (KeyCode kc in Hotkeys[k]) str += kc.ToString() + " ";
		// Debug.Log("Keycode true: " + str);
		return false;
	}
}

