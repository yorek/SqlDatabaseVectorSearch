﻿using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI.Chat;
using SqlDatabaseVectorSearch.Models;
using SqlDatabaseVectorSearch.Settings;

namespace SqlDatabaseVectorSearch.Services;

public class ChatService(IChatCompletionService chatCompletionService, TokenizerService tokenizerService, HybridCache cache, IOptions<AppSettings> appSettingsOptions)
{
    private readonly AppSettings appSettings = appSettingsOptions.Value;

    public async Task<ChatResponse> CreateQuestionAsync(Guid conversationId, string question)
    {
        var chat = await GetChatHistoryAsync(conversationId);

        var embeddingQuestion = $"""
            Reformulate the following question taking into account the context of the chat to perform embeddings search:
            ---
            {question}
            ---
            You must reformulate the question in the same language of the user's question. For example, it the user asks a question in English, the answer must be in English.
            Never add "in this chat", "in the context of this chat", "in the context of our conversation", "search for" or something like that in your answer.
            """;

        chat.AddUserMessage(embeddingQuestion);

        var reformulatedQuestion = await chatCompletionService.GetChatMessageContentAsync(chat)!;
        chat.AddAssistantMessage(reformulatedQuestion.Content!);

        await UpdateCacheAsync(conversationId, chat);

        var tokenUsage = GetTokenUsage(reformulatedQuestion);

        return new(reformulatedQuestion.Content!, tokenUsage);
    }

    public async Task<ChatResponse> AskQuestionAsync(Guid conversationId, IEnumerable<string> chunks, string question)
    {
        var chat = CreateChatAsync(chunks, question);

        var answer = await chatCompletionService.GetChatMessageContentAsync(chat, new AzureOpenAIPromptExecutionSettings
        {
            MaxTokens = appSettings.MaxOutputTokens
        });

        // Add question and answer to the chat history.
        await SetChatHistoryAsync(conversationId, question, answer.Content!);

        var tokenUsage = GetTokenUsage(answer);

        return new(answer.Content!, tokenUsage);
    }

    public async IAsyncEnumerable<ChatResponse> AskStreamingAsync(Guid conversationId, IEnumerable<string> chunks, string question)
    {
        var chat = CreateChatAsync(chunks, question);

        var answer = new StringBuilder();
        await foreach (var token in chatCompletionService.GetStreamingChatMessageContentsAsync(chat, new AzureOpenAIPromptExecutionSettings
        {
            MaxTokens = appSettings.MaxOutputTokens
        }))
        {
            if (!string.IsNullOrEmpty(token.Content))
            {
                yield return new(token.Content);
                answer.Append(token.Content);
            }
            else if (token.Content is null)
            {
                // Token usage is returned in the last message, when the Content is null.
                var tokenUsage = GetTokenUsage(token);
                if (tokenUsage is not null)
                {
                    yield return new(null, tokenUsage);
                }
            }
        }

        // Add question and answer to the chat history.
        await SetChatHistoryAsync(conversationId, question, answer.ToString());
    }

    private static TokenUsage? GetTokenUsage(Microsoft.SemanticKernel.ChatMessageContent message)
    {
        if (message.InnerContent is ChatCompletion content && content.Usage is not null)
        {
            return new(content.Usage.InputTokenCount, content.Usage.OutputTokenCount);
        }

        return null;
    }

    private static TokenUsage? GetTokenUsage(Microsoft.SemanticKernel.StreamingChatMessageContent message)
    {
        if (message.InnerContent is StreamingChatCompletionUpdate content && content.Usage is not null)
        {
            return new(content.Usage.InputTokenCount, content.Usage.OutputTokenCount);
        }

        return null;
    }

    private ChatHistory CreateChatAsync(IEnumerable<string> chunks, string question)
    {
        var chat = new ChatHistory("""
            You can use only the information provided in this chat to answer questions. If you don't know the answer, reply suggesting to refine the question.
            For example, if the user asks "What is the capital of France?" and in this chat there isn't information about France, you should reply something like "This information isn't available in the given context".
            Never answer to questions that are not related to this chat.
            You must answer in the same language of the user's question. For example, it the user asks a question in English, the answer must be in English.
            """);

        var prompt = new StringBuilder($"""
            Answer the following question:
            ---
            {question}
            =====          
            Using the following information:

            """);

        var tokensAvailable = appSettings.MaxInputTokens
                              - tokenizerService.CountChatCompletionTokens(chat[0].ToString())    // System prompt.
                              - tokenizerService.CountChatCompletionTokens(prompt.ToString()) // Initial user prompt.
                              - appSettings.MaxOutputTokens;    // To ensure there is enough space for the answer.

        foreach (var chunk in chunks)
        {
            var text = $"---{Environment.NewLine}{chunk}";

            var tokenCount = tokenizerService.CountChatCompletionTokens(text);
            if (tokenCount > tokensAvailable)
            {
                // There isn't enough space to add the current chunk.
                break;
            }

            prompt.Append(text);

            tokensAvailable -= tokenCount;
            if (tokensAvailable <= 0)
            {
                // There isn't enough space to add more chunks.
                break;
            }
        }

        chat.AddUserMessage(prompt.ToString());
        return chat;
    }

    private async Task UpdateCacheAsync(Guid conversationId, ChatHistory chat)
        => await cache.SetAsync(conversationId.ToString(), chat);

    private async Task<ChatHistory> GetChatHistoryAsync(Guid conversationId)
    {
        var historyCache = await cache.GetOrCreateAsync(conversationId.ToString(),
        (cancellationToken) =>
        {
            return ValueTask.FromResult<ChatHistory>([]);
        });

        var chat = new ChatHistory(historyCache);
        return chat;
    }

    private async Task SetChatHistoryAsync(Guid conversationId, string question, string answer)
    {
        var history = await GetChatHistoryAsync(conversationId);

        history.AddUserMessage(question);
        history.AddAssistantMessage(answer);

        await UpdateCacheAsync(conversationId, history);
    }
}
