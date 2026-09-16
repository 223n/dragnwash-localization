using DragNWash.ModFramework.Dialogue;
using TMPro;

namespace DragNWashLocalization
{
    // Per-line translations for dialogue.
    //
    // The text hook only sees the English it is about to show, so an English
    // line spoken by two characters ("Wonderful!" from Ryan in level 1 and from
    // Alexander in level 5) can only ever get one translation. Drag'n Wash
    // ModFramework's dialogue library knows which line a component was just
    // handed; when that line's text arrives, LineResolution finds the pack's
    // row for that line - a line:X row wins over the hash row - even when a
    // game update edited the English. Anything else the component shows later
    // does not match and goes the normal way.
    internal static class LineIdContext
    {
        // The translation for what is arriving on this component, if the
        // component was just handed that line and the pack has a row for it.
        public static bool TryGetTranslation(TMP_Text component, string incoming, out string translation, out string lineId)
        {
            translation = null;
            lineId = null;
            if (!GameDialogue.TryGetLine(component, incoming, out DialogueLine line))
            {
                return false;
            }
            if (!LineResolution.TryGetTranslation(line, incoming, out translation, out LineMatch match) &&
                !TranslationStore.TryGetLineTranslation(line.LineId, out translation))
            {
                return false;
            }

            // OptionItem strikes an unavailable option through.
            const string StrikeOpen = "<s>", StrikeClose = "</s>";
            if (incoming.StartsWith(StrikeOpen, System.StringComparison.Ordinal) && incoming.EndsWith(StrikeClose, System.StringComparison.Ordinal))
            {
                translation = StrikeOpen + translation + StrikeClose;
            }
            lineId = line.LineId;
            return true;
        }
    }
}
