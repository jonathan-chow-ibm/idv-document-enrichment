"""Application configuration via pydantic-settings."""

from functools import lru_cache

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Pipeline configuration loaded from environment variables."""

    # Azure OpenAI — Agent 1 (classification) uses mini, Agent 2 (extraction) uses full
    openai_endpoint: str = ""
    openai_deployment: str = "gpt-4o"
    openai_mini_deployment: str = "gpt-4o-mini"
    openai_api_version: str = "2024-12-01-preview"

    # Azure AI Document Intelligence
    doc_intelligence_endpoint: str = ""

    # SharePoint
    sharepoint_site_id: str = ""
    sharepoint_drive_id: str = ""
    sharepoint_review_list_id: str = ""

    # Taxonomy
    taxonomy_blob_url: str = ""

    # Processing
    confidence_threshold_default: float = 0.80
    batch_max_concurrency: int = 10

    model_config = {
        "env_prefix": "AZURE_",
        "env_file": ".env",
        "extra": "ignore",
    }


@lru_cache
def get_settings() -> Settings:
    return Settings()
