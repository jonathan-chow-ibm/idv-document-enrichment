"""IDV Document Enrichment Pipeline — Azure Functions entry point."""

import azure.functions as func
import azure.durable_functions as df

app = df.DFApp(http_auth_level=func.AuthLevel.FUNCTION)

# Blueprints will be registered here as activities and orchestrators are implemented
# from orchestrators.document_orchestrator import main as doc_orch_bp
# from orchestrators.batch_orchestrator import main as batch_orch_bp
# from activities.extract_content import main as extract_bp
# from activities.classify_document import main as classify_bp
# app.register_blueprint(doc_orch_bp)
# app.register_blueprint(batch_orch_bp)
# app.register_blueprint(extract_bp)
# app.register_blueprint(classify_bp)
