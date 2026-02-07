namespace LisperFlow.TextInjection;

public interface ITypingCommand { }

public class TypeWordCommand : ITypingCommand
{
    public string Word { get; set; } = "";
}

public class CorrectionCommand : ITypingCommand
{
    public int Position { get; set; }
    public int CharactersToDelete { get; set; }
    public string OldText { get; set; } = "";
    public string NewText { get; set; } = "";
}
