using Azure.Identity;
using Microsoft.SemanticKernel;

namespace StartupExtensions;

public static class AiProviderRegistration
{
  public static void ConfigureAiProvider(this WebApplicationBuilder builder, ILogger logger)
  {
    var config = builder.Configuration;
    var aiHost = config["AIHost"] ?? "OpenAI";

    switch (aiHost)
    {
      case "github":
        {
          var modelId = GetRequired(config, "GITHUB_MODEL_NAME");
          var token = GetRequired(config, "GITHUB_TOKEN");
          var endpoint = new Uri("https://models.inference.ai.azure.com");

          builder.Services.AddAzureAIInferenceChatCompletion(
            modelId: modelId,
            apiKey: token,
            endpoint: endpoint
          );

          logger.LogInformation("AI provider: GitHub Inference API");
          break;
        }

      case "azureAIModelCatalog":
        {
          var modelId = GetRequired(config, "AZURE_MODEL_NAME");
          var apiKey = GetRequired(config, "AZURE_INFERENCE_KEY");
          var endpoint = new Uri(GetRequired(config, "AZURE_MODEL_ENDPOINT"));

          builder.Services.AddAzureAIInferenceChatCompletion(
            modelId: modelId,
            apiKey: apiKey,
            endpoint: endpoint
          );

          logger.LogInformation("AI provider: Azure AI Model Catalog");
          break;
        }

      case "local":
        {
          var localModelName = GetRequired(config, "LOCAL_MODEL_NAME");
          var localEndpoint = new Uri(GetRequired(config, "LOCAL_ENDPOINT"));

          builder.Services.AddOllamaChatCompletion(
            modelId: localModelName,
            endpoint: localEndpoint
          );

          logger.LogInformation("AI provider: Local Ollama model configured");
          break;
        }

      default:
        {
          var deployment = GetRequired(config, "AZURE_OPENAI_DEPLOYMENT");
          var endpoint = GetRequired(config, "AZURE_OPENAI_ENDPOINT");

          var apiKey = config["AZURE_OPENAI_KEY"];

          if (!string.IsNullOrWhiteSpace(apiKey))
          {
            // Use key-based auth (works anywhere, no azd required)
            builder.Services.AddKernel()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: deployment,
                    endpoint: endpoint,
                    apiKey: apiKey
                );
            logger.LogInformation("AI provider: Azure OpenAI (key)");
          }
          else
          {
            // Fall back to managed identity / DefaultAzureCredential
            builder.Services.AddKernel()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: deployment,
                    endpoint: endpoint,                        // base host only
                    credentials: new DefaultAzureCredential()  // note the parameter name: credentials
                );
            logger.LogInformation("AI provider: Azure OpenAI (DefaultAzureCredential)");
          }

          break;
        }
    }
  }

  private static string GetRequired(IConfiguration config, string key)
  {
    var value = config[key];
    if (string.IsNullOrWhiteSpace(value))
      throw new InvalidOperationException($"{key} is not set in configuration.");
    return value;
  }
}
