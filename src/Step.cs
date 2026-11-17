using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GhostMacro
{
    public enum StepAction { Click, DoubleClick, MouseDown, MouseUp, Move, Drag, Wheel, KeyCombo, KeyDown, KeyUp, TypeText, Wait }

    public enum MouseBtn { Left, Right, Middle }

    /// <summary>Makrodaki tek adım. DelayMs = bu adımdan ÖNCE beklenecek süre.</summary>
    public class Step
    {
        public StepAction Action;
        public MouseBtn Button;
        public int X, Y, X2, Y2;
        public bool AtCursor;
        /// <summary>Tekerlek: çentik sayısı (+ yukarı). Sürükleme: süre (ms).</summary>
        public int Amount;
        public string Keys = "";
        public string Text = "";
        public int DelayMs;
        public string Note = "";

        public Step Clone() => (Step)MemberwiseClone();

        public static readonly StepAction[] All = (StepAction[])Enum.GetValues(typeof(StepAction));

        public static string ActionName(StepAction a)
        {
            switch (a)
            {
                case StepAction.Click: return L.T("Tıkla", "Click");
                case StepAction.DoubleClick: return L.T("Çift tıkla", "Double-click");
                case StepAction.MouseDown: return L.T("Fare tuşuna bas (basılı tut)", "Mouse button down (hold)");
                case StepAction.MouseUp: return L.T("Fare tuşunu bırak", "Mouse button up");
                case StepAction.Move: return L.T("Fareyi taşı", "Move mouse");
                case StepAction.Drag: return L.T("Sürükle-bırak", "Drag and drop");
                case StepAction.Wheel: return L.T("Tekerlek", "Mouse wheel");
                case StepAction.KeyCombo: return L.T("Tuş / kombinasyon", "Key / combination");
                case StepAction.KeyDown: return L.T("Tuşa bas (basılı tut)", "Key down (hold)");
                case StepAction.KeyUp: return L.T("Tuşu bırak", "Key up");
                case StepAction.TypeText: return L.T("Metin yaz", "Type text");
                case StepAction.Wait: return L.T("Sadece bekle", "Wait only");
                default: return a.ToString();
            }
        }

        public static string ButtonName(MouseBtn b) =>
            b == MouseBtn.Right ? L.T("Sağ", "Right") : b == MouseBtn.Middle ? L.T("Orta", "Middle") : L.T("Sol", "Left");

        public static bool HasButton(StepAction a) =>
            a == StepAction.Click || a == StepAction.DoubleClick || a == StepAction.MouseDown ||
            a == StepAction.MouseUp || a == StepAction.Drag;

        public static bool HasPosition(StepAction a) =>
            HasButton(a) || a == StepAction.Move || a == StepAction.Wheel;

        public static bool AllowsAtCursor(StepAction a) =>
            a == StepAction.Click || a == StepAction.DoubleClick || a == StepAction.MouseDown ||
            a == StepAction.MouseUp || a == StepAction.Wheel;

        public static bool IsKeyAction(StepAction a) =>
            a == StepAction.KeyCombo || a == StepAction.KeyDown || a == StepAction.KeyUp;

        [XmlIgnore]
        public string ActionText => HasButton(Action) ? $"{ActionName(Action)} ({ButtonName(Button)})" : ActionName(Action);

        [XmlIgnore]
        public string Detail
        {
            get
            {
                string pos = AtCursor && AllowsAtCursor(Action) ? L.T("imlecin bulunduğu yer", "wherever the cursor is") : $"X {X}, Y {Y}";
                switch (Action)
                {
                    case StepAction.Drag:
                        return $"X {X}, Y {Y}  →  X {X2}, Y {Y2}   ({Amount} ms)";
                    case StepAction.Wheel:
                        return $"{(Amount >= 0 ? "+" : "")}{Amount} {L.T("çentik", "notch")} " +
                               $"({(Amount >= 0 ? L.T("yukarı", "up") : L.T("aşağı", "down"))}) — {pos}";
                    case StepAction.KeyCombo:
                    case StepAction.KeyDown:
                    case StepAction.KeyUp:
                        return Keys;
                    case StepAction.TypeText:
                        var t = (Text ?? "").Replace("\n", " ⏎ ");
                        if (t.Length > 80) t = t.Substring(0, 77) + "...";
                        return "“" + t + "”";
                    case StepAction.Wait:
                        return "";
                    default:
                        return pos;
                }
            }
        }
    }

    public class MacroFile
    {
        public int Version = 1;
        public List<Step> Steps = new List<Step>();
    }

    public class AppSettings
    {
        public string Lang;
        public bool FullMode;
        public bool RecordKeys = true;
        public bool RealDelays = true;
        public int FixedDelay = 500;
        public int Repeat = 1;
        public bool Infinite;
        public int RepeatGap;
        public int Speed = 100;
        public int Jitter;
        public int Smooth;
        public bool StopOnMove = true;
        public bool Minimize = true;
        public bool TopMost;
        public int DefaultDelay = 500;
        public string HkRecord = "F9";
        public string HkPlay = "F10";
        public string HkStop = "F12";
        public string HkCapture = "F8";
        public string LastFile;
        public bool Dirty;
        public List<Step> Steps = new List<Step>();
    }
}
