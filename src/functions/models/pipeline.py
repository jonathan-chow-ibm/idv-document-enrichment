"""Pydantic models for pipeline data contracts."""

from __future__ import annotations

from enum import Enum

from pydantic import BaseModel, Field


class ProcessingSource(str, Enum):
    TRIGGER = "trigger"
    BATCH = "batch"


class RoutingDecision(str, Enum):
    WRITE = "write"
    REVIEW = "review"


class DocumentType(str, Enum):
    LEASE_AGREEMENT = "Lease Agreement"
    OFFER_MEMORANDUM = "Offer Memorandum"
    MARKET_REPORT = "Market Report"
    PURCHASE_AGREEMENT = "Purchase Agreement"
    LETTER_OF_INTENT = "Letter of Intent"
    FINANCIAL_ANALYSIS = "Financial Analysis"
    DUE_DILIGENCE = "Due Diligence"
    CORRESPONDENCE = "Correspondence"
    PRESENTATION = "Presentation"
    OTHER = "Other"


class QueueMessage(BaseModel):
    """Message placed on the processing queue for each document."""

    document_id: str
    site_id: str
    drive_id: str
    item_id: str
    file_name: str
    file_url: str
    content_type: str
    modified_date_time: str
    source: ProcessingSource
    batch_id: str | None = None
    attempt_number: int = 1


class KeyValuePair(BaseModel):
    key: str
    value: str
    confidence: float


class ExtractionResult(BaseModel):
    """Output from Azure AI Document Intelligence extraction."""

    text: str
    page_count: int
    text_length: int
    key_value_pairs: list[KeyValuePair] = Field(default_factory=list)
    language: str = "unknown"


# --- Agent 1: Document Type Classification ---


class TypeClassificationResult(BaseModel):
    """Agent 1 output: document type classification."""

    document_type: DocumentType
    confidence: float = Field(ge=0.0, le=1.0)
    reasoning: str


# --- Agent 2: Type-Specific Metadata Extraction ---


class CategoryClassification(BaseModel):
    """Extraction result for a single metadata field."""

    value: str
    confidence: float = Field(ge=0.0, le=1.0)
    reasoning: str


class SuggestedField(BaseModel):
    """An additional metadata field discovered by Agent 2 outside the defined schema."""

    key: str
    value: str
    confidence: float = Field(ge=0.0, le=1.0)


class MetadataExtractionResult(BaseModel):
    """Agent 2 output: common fields + type-specific fields + suggestions."""

    deal_type: CategoryClassification = Field(alias="dealType")
    submarket: CategoryClassification
    counterparty: CategoryClassification
    confidentiality: CategoryClassification
    type_specific_fields: dict[str, CategoryClassification] = Field(default_factory=dict)
    suggested_fields: list[SuggestedField] = Field(default_factory=list)

    model_config = {"populate_by_name": True}


# --- Combined Pipeline Result ---


class ProcessingMetrics(BaseModel):
    extraction_duration_ms: int = 0
    classification_duration_ms: int = 0
    metadata_extraction_duration_ms: int = 0
    total_duration_ms: int = 0
    classification_input_tokens: int = 0
    classification_output_tokens: int = 0
    extraction_input_tokens: int = 0
    extraction_output_tokens: int = 0


class EnrichmentResult(BaseModel):
    """Combined output from the full two-agent pipeline."""

    document_id: str
    file_name: str
    extraction: ExtractionResult
    type_classification: TypeClassificationResult
    metadata: MetadataExtractionResult
    processing_metrics: ProcessingMetrics
    routing_decision: RoutingDecision
    low_confidence_categories: list[str] = Field(default_factory=list)


class DocumentResult(BaseModel):
    """Complete processing result for a single document."""

    document_id: str
    file_name: str
    extraction_result: ExtractionResult
    classification: ClassificationResult
    overall_confidence: float
    routing_decision: RoutingDecision
    low_confidence_categories: list[str] = Field(default_factory=list)
    processing_metrics: ProcessingMetrics = Field(default_factory=ProcessingMetrics)
    processed_at: datetime = Field(default_factory=datetime.utcnow)
