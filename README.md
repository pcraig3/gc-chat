<div align="center">
  <h1>GC Chat 🇨🇦</h1>
  <p><em>by Paul Craig</em></p>
</div>

Note that this repo was originally forked from [`ai-chat-quickstart-csharp`](https://github.com/Azure-Samples/ai-chat-quickstart-csharp)

GC Chat is an open-source chatbot interface purpose-built for the Government of Canada. GC Chat is accessible, bilingual, GoC-branded, and mobile-ready.

GC Chat is an [ASP.NET app](https://dotnet.microsoft.com/en-us/apps/aspnet) that expects to be deployed inside of Azure.

The project includes all the infrastructure and configuration needed to provision Azure OpenAI resources and deploy the app to [Azure Container Apps](https://learn.microsoft.com/azure/container-apps/overview) using the [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/overview).

## Table of Contents

- [Features](#features)
- [Architecture diagram](#architecture-diagram)
- [Getting started](#getting-started)
  - [Local Environment - Visual Studio or VS Code](#local-environment)
  - [GitHub Codespaces](#github-codespaces)
  - [VS Code Dev Containers](#vs-code-dev-containers)
- [Deploying](#deploying)
- [Development server](#development-server)
- [Guidance](#guidance)
  - [Costs](#costs)
  - [Security Guidelines](#security-guidelines)
- [Resources](#resources)

## Features

- An [ASP.NET Core](https://dotnet.microsoft.com/en-us/apps/aspnet) that uses [Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/) package to access language models to generate responses to user messages.
- A basic HTML/JS frontend that streams responses from the backend using JSON over a [ReadableStream](https://developer.mozilla.org/en-US/docs/Web/API/ReadableStream).
- A Blazor frontend that streams responses from the backend.
- [Bicep files](https://docs.microsoft.com/azure/azure-resource-manager/bicep/) for provisioning Azure resources, including Azure OpenAI, Azure Container Apps, Azure Container Registry, Azure Log Analytics, and RBAC roles.
- Using the OpenAI gpt-4o-mini model through Azure OpenAI.
- Support for using [local LLMs](/docs/local_ollama.md) or GitHub Models during development.

## Architecture diagram

![Diagram of GC Chat’s architecture in Azure: documents are uploaded to Blob Storage, indexed with Azure AI Search and text-embedding-3-large, and then queried by the container app. The app sends questions and documents to a GPT-4o chat model, returns answers to the user, and stores conversations in Cosmos DB.](./docs/arch-diagram.png)

## Getting started

You have a few options for getting started with this app.

We will focus on two: local development and deployment on Azure.

### Local Environment

To run the app locally, follow these directions:

1. Make sure the following tools are installed:

   - [.NET 9](https://dotnet.microsoft.com/downloads/)
   - [Git](https://git-scm.com/downloads)
   - [Azure Developer CLI (azd)](https://aka.ms/install-azd)
   - [VS Code](https://code.visualstudio.com/Download) or [Visual Studio](https://visualstudio.microsoft.com/downloads/)
     - If using VS Code, install the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

2. If you're using Visual Studio, open the src/ai-chat-quickstart.sln solution file. If you're using VS Code, open the src folder.

3. Continue with the [deploying steps](#deploying).

Note that if this app is already running in an Azure environment, you can connect to backend services locally and run on your desktop.

## Deploying

Once you've opened the project in [Codespaces](#github-codespaces), in [Dev Containers](#vs-code-dev-containers), or [locally](#local-environment), you can deploy it to Azure.

### Azure account setup

1. Make sure you have an azure account and access to [environment]

2. Check that you have the necessary permissions:

   - Your Azure account must have `Microsoft.Authorization/roleAssignments/write` permissions, such as [Role Based Access Control Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#role-based-access-control-administrator-preview), [User Access Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#user-access-administrator), or [Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#owner). If you don't have subscription-level permissions, you must be granted [RBAC](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#role-based-access-control-administrator-preview) for an existing resource group and [deploy to that existing group](/docs/deploy_existing.md#resource-group).
   - Your Azure account also needs `Microsoft.Resources/deployments/write` permissions on the subscription level.

### Deploying with azd

From a Terminal window, open the folder with the clone of this repo and run the following commands.

1. Login to Azure:

   ```shell
   azd auth login
   ```

2. Provision and deploy all the resources:

   ```shell
   azd up
   ```

   It will prompt you to provide an `azd` environment name (like "chat-app"), select a subscription from your Azure account, and select a [location where OpenAI is available](https://azure.microsoft.com/explore/global-infrastructure/products-by-region/?products=cognitive-services&regions=all) (like "canadaeast"). Also useful to reference [which models are available in Canada](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models?tabs=global-standard%2Cstandard-chat-completions#global-standard-model-availability). Then it will provision the resources in your account and deploy the latest code. If you get an error or timeout with deployment, changing the location can help, as there may be availability constraints for the OpenAI resource.

3. When `azd` has finished deploying, you'll see an endpoint URI in the command output. Visit that URI, and you should see the chat app! 🎉

4. When you've made any changes to the app code, you can just run:

   ```shell
   azd deploy
   ```

## Development server

In order to run this app, you need to either have an Azure OpenAI account deployed (from the [deploying steps](#deploying)), use the [Azure AI Model Catalog](https://learn.microsoft.com/en-us/azure/machine-learning/concept-model-catalog?view=azureml-api-2), or use a [local LLM server](/docs/local_ollama.md).

After deployment, Azure OpenAI is configured for you using [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets). If you could not run the deployment steps here, or you want to use different models, you can manually update the settings in `appsettings.local.json`. **Important:** This file is only for local development and this sample includes it in the `.gitignore` file so changes to it will be ignored. Do not check your secure keys into source control!

1. If you want to use an existing Azure OpenAI deployment, you modify the `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_DEPLOYMENT` configuration settings in the `appsettings.local.json` file.

2. For use with local models, change `AIHost` to "local" in the `appsettings.local.json` file and change `LOCAL_MODELS_ENDPOINT` and `LOCAL_MODELS_NAME` to match the local server. See [local LLM server](/docs/local_ollama.md) for more information.

3. To use the Azure AI Model Catalog, change `AIHost` to "azureAIModelCatalog" in the `appsettings.local.json` file. Change `AZURE_INFERENCE_KEY`, `AZURE_MODEL_NAME`, and `AZURE_MODEL_ENDPOINT` settings to match your configuration in the Azure AI Model Catalog.

4. Start the project:

   **If using Visual Studio**, choose the `Debug > Start Debugging` menu.
   **If using VS Code or GitHub CodeSpaces\***, choose the `Run > Start Debugging` menu.
   Finally, if using the command line, run the following from the project directory:

   ```shell
   dotnet run
   ```

   This will start the app on port 5153, and you can access it at `http://localhost:5153`.

## Guidance

### Security Guidelines

This template uses [Managed Identity](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview) for authenticating to the Azure OpenAI service.

Additionally, we have added a [GitHub Action](https://github.com/microsoft/security-devops-action) that scans the infrastructure-as-code files and generates a report containing any detected issues. To ensure continued best practices in your own repository, we recommend that anyone creating solutions based on our templates ensure that the [Github secret scanning](https://docs.github.com/code-security/secret-scanning/about-secret-scanning) setting is enabled.

You may want to consider additional security measures, such as:

- Protecting the Azure Container Apps instance with a [firewall](https://learn.microsoft.com/azure/container-apps/waf-app-gateway) and/or [Virtual Network](https://learn.microsoft.com/azure/container-apps/networking?tabs=workload-profiles-env%2Cazure-cli).

## Resources

- [RAG chat with Azure AI Search + C#/.NET](https://github.com/Azure-Samples/azure-search-openai-demo-csharp/): A more advanced chat app that uses Azure AI Search to ground responses in domain knowledge. Includes user authentication with Microsoft Entra as well as data access controls.
- [Develop .NET Apps with AI Features](https://learn.microsoft.com/en-us/dotnet/ai/get-started/dotnet-ai-overview)
