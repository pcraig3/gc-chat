#!/bin/bash

# Azure Search Setup Script
# This script creates a complete Azure Search setup with index, data source, skillset, and indexer
# Based on the "Import and vectorize data" wizard output

set -e  # Exit on any error

# Function to make API calls with better error handling
make_api_call() {
    local method=$1
    local endpoint=$2
    local data=$3
    
    # Add API version to endpoint
    if [[ "$endpoint" == *"?"* ]]; then
        endpoint="${endpoint}&api-version=${API_VERSION}"
    else
        endpoint="${endpoint}?api-version=${API_VERSION}"
    fi
    
    echo "Making $method request to: $SEARCH_SERVICE_URL$endpoint"
    
    if [ -n "$data" ]; then
        # Save JSON to temp file for debugging
        echo "$data" > /tmp/azure_search_payload.json
        echo "Request payload saved to /tmp/azure_search_payload.json"
        
        response=$(curl -X "$method" \
             -H "Content-Type: application/json" \
             -H "api-key: $AZURE_SEARCH_ADMIN_KEY" \
             -d "$data" \
             "$SEARCH_SERVICE_URL$endpoint" \
             --write-out "HTTPSTATUS:%{http_code}" \
             --silent --show-error)
    else
        response=$(curl -X "$method" \
             -H "api-key: $AZURE_SEARCH_ADMIN_KEY" \
             "$SEARCH_SERVICE_URL$endpoint" \
             --write-out "HTTPSTATUS:%{http_code}" \
             --silent --show-error)
    fi
    
    # Extract HTTP status and body
    http_code=$(echo "$response" | tr -d '\n' | sed -e 's/.*HTTPSTATUS://')
    body=$(echo "$response" | sed -e 's/HTTPSTATUS:.*//g')
    
    echo "HTTP Status: $http_code"
    
    if [ "$http_code" -ge 400 ]; then
        echo "Error Response Body: $body"
        echo "Request failed with status $http_code"
        exit 1
    fi
    
    echo "Response: $body"
}

# Function to check if a resource exists
resource_exists() {
    local resource_type=$1
    local resource_name=$2
    
    response=$(curl -X "GET" \
         -H "api-key: $AZURE_SEARCH_ADMIN_KEY" \
         "$SEARCH_SERVICE_URL/$resource_type/$resource_name?api-version=$API_VERSION" \
         --write-out "HTTPSTATUS:%{http_code}" \
         --silent --show-error)
    
    http_code=$(echo "$response" | tr -d '\n' | sed -e 's/.*HTTPSTATUS://')
    
    if [ "$http_code" -eq 200 ]; then
        return 0  # exists
    else
        return 1  # doesn't exist
    fi
}

# Check if required environment variables are set
if [ -z "$AZURE_SEARCH_SERVICE_NAME" ] || [ -z "$AZURE_OPENAI_ENDPOINT" ] || [ -z "$AZURE_OPENAI_EMBEDDING_DEPLOYMENT" ] || [ -z "$STORAGE_ACCOUNT_NAME" ] || [ -z "$STORAGE_CONTAINER_NAME" ]; then
    echo "Error: Required environment variables are not set."
    echo "Missing variables from: AZURE_SEARCH_SERVICE_NAME, AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_EMBEDDING_DEPLOYMENT, STORAGE_ACCOUNT_NAME, STORAGE_CONTAINER_NAME"
    exit 1
fi

# Get Azure Search admin key
echo "Retrieving Azure Search admin key..."
AZURE_SEARCH_ADMIN_KEY=$(az search admin-key show --resource-group "$AZURE_RESOURCE_GROUP" --service-name "$AZURE_SEARCH_SERVICE_NAME" --query "primaryKey" --output tsv)

if [ -z "$AZURE_SEARCH_ADMIN_KEY" ]; then
    echo "Error: Could not retrieve Azure Search admin key. Make sure you're logged in with 'az login'"
    exit 1
fi

# Configuration
SEARCH_SERVICE_URL="$AZURE_SEARCH_SERVICE_ENDPOINT"
API_VERSION="2024-11-01-preview"  # Preview API version that supports all wizard features
TIMESTAMP=$(date +%s)
INDEX_NAME="rag-${TIMESTAMP}"
DATASOURCE_NAME="${INDEX_NAME}-datasource"
SKILLSET_NAME="${INDEX_NAME}-skillset"
INDEXER_NAME="${INDEX_NAME}-indexer"
CONTAINER_NAME="$STORAGE_CONTAINER_NAME"

echo "Creating Azure Search resources with timestamp: $TIMESTAMP"
echo "Search Service: $SEARCH_SERVICE_URL"
echo "Index Name: $INDEX_NAME"
echo "API Version: $API_VERSION"
echo "Embedding Dimensions: ${AZURE_OPENAI_EMBEDDING_DIMENSIONS:-3072}"
echo "OpenAI Endpoint: $AZURE_OPENAI_ENDPOINT"
echo "Embedding Deployment: $AZURE_OPENAI_EMBEDDING_DEPLOYMENT"

# Check if any indexer already exists
echo "Checking for existing indexers..."
existing_indexers=$(curl -X "GET" \
     -H "api-key: $AZURE_SEARCH_ADMIN_KEY" \
     "$SEARCH_SERVICE_URL/indexers?api-version=$API_VERSION" \
     --silent)

indexer_count=$(echo "$existing_indexers" | grep -o '"name"' | wc -l)

if [ "$indexer_count" -gt 0 ]; then
    echo "⚠️  Found $indexer_count existing indexer(s). Skipping creation to avoid duplicates."
    echo "Existing indexers:"
    echo "$existing_indexers" | grep -o '"name":"[^"]*' | sed 's/"name":"/  - /' | sed 's/"$//'
    echo ""
    echo "If you want to create new resources, please delete existing indexers first."
    exit 0
fi

# Grant Azure Search service permissions to Azure OpenAI
echo "Setting up permissions..."
echo "Granting Search Service access to Azure OpenAI..."

# Default AOAI RG to the app RG if not provided
OPENAI_RG="${AZURE_OPENAI_RESOURCE_GROUP:-$AZURE_RESOURCE_GROUP}"

# Parse the AOAI account name from the endpoint (strip domain AND any trailing slash)
AZURE_OPENAI_RESOURCE_NAME=$(echo "$AZURE_OPENAI_ENDPOINT" | sed -E 's|https?://||; s|\.openai\.azure\.com/?$||')

# Grant Cognitive Services OpenAI User role (same subscription)
az role assignment create \
  --assignee "$AZURE_SEARCH_SERVICE_PRINCIPAL_ID" \
  --role "Cognitive Services OpenAI User" \
  --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$OPENAI_RG/providers/Microsoft.CognitiveServices/accounts/$AZURE_OPENAI_RESOURCE_NAME" \
  || echo "Note: OpenAI role assignment may already exist"

# Grant Storage Blob Data Reader role to Search Service (unchanged)
echo "Granting Search Service access to Storage Account..."
az role assignment create \
  --assignee "$AZURE_SEARCH_SERVICE_PRINCIPAL_ID" \
  --role "Storage Blob Data Reader" \
  --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$AZURE_RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT_NAME" \
  || echo "Note: Storage role assignment may already exist"

echo "✓ Permissions configured"
echo "Waiting 20s for RBAC to propagate..."
sleep 20

# Test search service connectivity
echo "Testing search service connectivity..."
make_api_call "GET" "/indexes" ""
echo "✓ Search service is accessible"

# 1. Create the Search Index (matching wizard output exactly)
echo "Creating search index: $INDEX_NAME"
INDEX_JSON=$(cat <<EOF
{
  "name": "$INDEX_NAME",
  "fields": [
    {
      "name": "chunk_id",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": true,
      "facetable": false,
      "key": true,
      "analyzer": "keyword",
      "synonymMaps": []
    },
    {
      "name": "parent_id",
      "type": "Edm.String",
      "searchable": false,
      "filterable": true,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "key": false,
      "synonymMaps": []
    },
    {
      "name": "chunk",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "key": false,
      "synonymMaps": []
    },
    {
      "name": "title",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "key": false,
      "synonymMaps": []
    },
    {
      "name": "text_vector",
      "type": "Collection(Edm.Single)",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "key": false,
      "dimensions": ${AZURE_OPENAI_EMBEDDING_DIMENSIONS:-3072},
      "vectorSearchProfile": "$INDEX_NAME-azureOpenAi-text-profile",
      "synonymMaps": []
    },
    {
      "name": "culture",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "key": false,
      "synonymMaps": []
    },
    {
      "name": "type",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "key": false,
      "synonymMaps": []
    },
    {
      "name": "size",
      "type": "Edm.Int64",
      "searchable": false,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "key": false,
      "synonymMaps": []
    },
    {
      "name": "path",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "stored": true,
      "sortable": false,
      "facetable": false,
      "key": false,
      "synonymMaps": []
    }
  ],

  "similarity": {
    "@odata.type": "#Microsoft.Azure.Search.BM25Similarity"
  },
  "semantic": {
    "defaultConfiguration": "$INDEX_NAME-semantic-configuration",
    "configurations": [
      {
        "name": "$INDEX_NAME-semantic-configuration",

        "prioritizedFields": {
          "titleField": {
            "fieldName": "title"
          },
          "prioritizedContentFields": [
            {
              "fieldName": "chunk"
            }
          ],
          "prioritizedKeywordsFields": []
        }
      }
    ]
  },
  "vectorSearch": {
    "algorithms": [
      {
        "name": "$INDEX_NAME-algorithm",
        "kind": "hnsw",
        "hnswParameters": {
          "metric": "cosine",
          "m": 4,
          "efConstruction": 400,
          "efSearch": 500
        }
      }
    ],
    "profiles": [
      {
        "name": "$INDEX_NAME-azureOpenAi-text-profile",
        "algorithm": "$INDEX_NAME-algorithm",
        "vectorizer": "$INDEX_NAME-azureOpenAi-text-vectorizer"
      }
    ],
    "vectorizers": [
      {
        "name": "$INDEX_NAME-azureOpenAi-text-vectorizer",
        "kind": "azureOpenAI",
        "azureOpenAIParameters": {
          "resourceUri": "$AZURE_OPENAI_ENDPOINT",
          "deploymentId": "$AZURE_OPENAI_EMBEDDING_DEPLOYMENT",
          "modelName": "text-embedding-3-large"
        }
      }
    ],
    "compressions": []
  }
}
EOF
)

make_api_call "POST" "/indexes" "$INDEX_JSON"
echo "✓ Search index created successfully"

# Get storage account connection string
echo "Retrieving storage account connection string..."
STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
    --name "$STORAGE_ACCOUNT_NAME" \
    --resource-group "$AZURE_RESOURCE_GROUP" \
    --query connectionString \
    --output tsv)

# Create data source with connection string
DATASOURCE_JSON=$(cat <<EOF
{
  "name": "$DATASOURCE_NAME",
  "type": "azureblob",
  "credentials": {
    "connectionString": "$STORAGE_CONNECTION_STRING"
  },
  "container": {
    "name": "$CONTAINER_NAME"
  },
  "dataDeletionDetectionPolicy": {
    "@odata.type": "#Microsoft.Azure.Search.NativeBlobSoftDeleteDeletionDetectionPolicy"
  }
}
EOF
)

make_api_call "POST" "/datasources" "$DATASOURCE_JSON"
echo "✓ Data source created successfully"

# 3. Create the Skillset (matching wizard output exactly, including the unit property)
echo "Creating skillset: $SKILLSET_NAME"
SKILLSET_JSON=$(cat <<EOF
{
  "name": "$SKILLSET_NAME",
  "description": "Skillset to chunk documents and generate embeddings",
  "skills": [
    {
      "@odata.type": "#Microsoft.Skills.Text.SplitSkill",
      "name": "#1",
      "description": "Split skill to chunk documents",
      "context": "/document",
      "defaultLanguageCode": "en",
      "textSplitMode": "pages",
      "maximumPageLength": 2000,
      "pageOverlapLength": 500,
      "maximumPagesToTake": 0,
      "unit": "characters",
      "inputs": [
        {
          "name": "text",
          "source": "/document/content",
          "inputs": []
        }
      ],
      "outputs": [
        {
          "name": "textItems",
          "targetName": "pages"
        }
      ]
    },
    {
      "@odata.type": "#Microsoft.Skills.Text.AzureOpenAIEmbeddingSkill",
      "name": "#2",
      "context": "/document/pages/*",
      "resourceUri": "$AZURE_OPENAI_ENDPOINT",
      "deploymentId": "$AZURE_OPENAI_EMBEDDING_DEPLOYMENT",
      "dimensions": ${AZURE_OPENAI_EMBEDDING_DIMENSIONS:-3072},
      "modelName": "text-embedding-3-large",
      "inputs": [
        {
          "name": "text",
          "source": "/document/pages/*",
          "inputs": []
        }
      ],
      "outputs": [
        {
          "name": "embedding",
          "targetName": "text_vector"
        }
      ]
    }
  ],
  "indexProjections": {
    "selectors": [
      {
        "targetIndexName": "$INDEX_NAME",
        "parentKeyFieldName": "parent_id",
        "sourceContext": "/document/pages/*",
        "mappings": [
          {
            "name": "text_vector",
            "source": "/document/pages/*/text_vector",
            "inputs": []
          },
          {
            "name": "chunk",
            "source": "/document/pages/*",
            "inputs": []
          },
          {
            "name": "title",
            "source": "/document/title",
            "inputs": []
          },
          {
            "name": "culture",
            "source": "/document/metadata_language",
            "inputs": []
          },
          {
            "name": "type",
            "source": "/document/metadata_storage_content_type",
            "inputs": []
          },
          {
            "name": "size",
            "source": "/document/metadata_storage_size",
            "inputs": []
          },
          {
            "name": "path",
            "source": "/document/metadata_storage_path",
            "inputs": []
          }
        ]
      }
    ],
    "parameters": {
      "projectionMode": "skipIndexingParentDocuments"
    }
  }
}
EOF
)

make_api_call "POST" "/skillsets" "$SKILLSET_JSON"
echo "✓ Skillset created successfully"

# 4. Create the Indexer (matching wizard output exactly)
echo "Creating indexer: $INDEXER_NAME"
INDEXER_JSON=$(cat <<EOF
{
  "name": "$INDEXER_NAME",
  "description": null,
  "dataSourceName": "$DATASOURCE_NAME",
  "skillsetName": "$SKILLSET_NAME",
  "targetIndexName": "$INDEX_NAME",
  "disabled": null,
  "schedule": null,
  "parameters": {
    "batchSize": null,
    "maxFailedItems": null,
    "maxFailedItemsPerBatch": null,
    "configuration": {
      "dataToExtract": "contentAndMetadata",
      "parsingMode": "default"
    }
  },
  "fieldMappings": [
    {
      "sourceFieldName": "metadata_storage_name",
      "targetFieldName": "title",
      "mappingFunction": null
    }
  ],
  "outputFieldMappings": [],
  "cache": null,
  "encryptionKey": null
}
EOF
)

make_api_call "POST" "/indexers" "$INDEXER_JSON"
echo "✓ Indexer created successfully"

echo ""
echo "🎉 Azure Search setup completed successfully!"
echo "Index Name: $INDEX_NAME"
echo "Data Source: $DATASOURCE_NAME"
echo "Skillset: $SKILLSET_NAME"
echo "Indexer: $INDEXER_NAME"
echo ""
echo "Setup is complete! You can now:"
echo "1. Upload documents to your storage container: $CONTAINER_NAME"
echo "2. Run the indexer manually when ready:"
echo "   curl -X POST -H \"api-key: $AZURE_SEARCH_ADMIN_KEY\" \"$SEARCH_SERVICE_URL/indexers/$INDEXER_NAME/run?api-version=$API_VERSION\""
echo "3. Check indexer status:"
echo "   curl -H \"api-key: $AZURE_SEARCH_ADMIN_KEY\" \"$SEARCH_SERVICE_URL/indexers/$INDEXER_NAME/status?api-version=$API_VERSION\""
