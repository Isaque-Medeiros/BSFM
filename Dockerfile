# ----------------------------------------------------------------------
# Dockerfile para Chat com Visao Computacional (Groq + Streamlit)
# Otimizado para deploy no Render
# ----------------------------------------------------------------------

# --- Imagem base ---
FROM python:3.11-slim

# --- Variaveis de ambiente ---
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1

# --- Diretorio de trabalho ---
WORKDIR /app

# --- Copiar dependencias primeiro (cache layer) ---
COPY requirements.txt .

# --- Instalar dependencias ---
RUN pip install --no-cache-dir -r requirements.txt

# --- Copiar o codigo da aplicacao ---
COPY app.py .

# --- Expor a porta (definida pelo Render via $PORT) ---
EXPOSE $PORT

# --- Comando de inicializacao ---
CMD streamlit run app.py --server.port $PORT --server.address 0.0.0.0
