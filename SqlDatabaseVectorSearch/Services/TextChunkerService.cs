﻿using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Text;
using SqlDatabaseVectorSearch.Settings;

namespace SqlDatabaseVectorSearch.Services;

public class TextChunkerService(TokenizerService tokenizerService, IOptions<AppSettings> appSettingsOptions)
{
    private readonly AppSettings appSettings = appSettingsOptions.Value;

    public IList<string> Split(string text)
    {
        var lines = TextChunker.SplitPlainTextLines(text, appSettings.MaxTokensPerLine, tokenizerService.CountChatCompletionTokens);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, appSettings.MaxTokensPerParagraph, appSettings.OverlapTokens, tokenCounter: tokenizerService.CountChatCompletionTokens);

        return paragraphs;
    }
}
