namespace ProjectService.BusinessLayer.Abstractions;

public interface IAiManager
{
    Task<string> ProcessUserText(string userText, string mode = "paraphrase");
}