# 🤖 Chat com Visão Computacional (Groq + Llama 3.2 Vision)

Aplicação Streamlit que permite fazer upload de imagens e conversar sobre elas usando o modelo de visão **Llama 3.2 Vision** da **Groq**.

## 🚀 Funcionalidades

- Upload de imagens (PNG, JPG, JPEG)
- Interface de chat interativa para perguntas sobre a imagem
- Respostas geradas pelo modelo `llama-3.2-11b-vision-preview` da Groq
- Tratamento de erros (rate limit, timeout, etc.)
- Botão "Nova Conversa" para reiniciar o chat

## 📋 Pré-requisitos

- **Python 3.11+** (para execução local)
- **Docker** (para execução containerizada)
- Uma **chave de API da Groq** ([https://console.groq.com](https://console.groq.com))

## 🐍 Execução Local

### 1. Clone o repositório e entre na pasta

```bash
cd NMday
```

### 2. Crie um ambiente virtual (opcional, mas recomendado)

```bash
python -m venv venv
```

- **Windows:** `venv\Scripts\activate`
- **Linux/Mac:** `source venv/bin/activate`

### 3. Instale as dependências

```bash
pip install -r requirements.txt
```

### 4. Configure a variável de ambiente

**Windows (PowerShell):**
```powershell
$env:GROQ_API_KEY="sua-chave-aqui"
```

**Windows (CMD):**
```cmd
set GROQ_API_KEY=sua-chave-aqui
```

**Linux/Mac:**
```bash
export GROQ_API_KEY="sua-chave-aqui"
```

> 💡 Você também pode criar um arquivo `.env` na raiz do projeto com:
> ```
> GROQ_API_KEY=sua-chave-aqui
> ```

### 5. Execute a aplicação

```bash
streamlit run app.py
```

Acesse no navegador: [http://localhost:8501](http://localhost:8501)

## 🐳 Execução com Docker

### 1. Build da imagem

```bash
docker build -t chat-visao-groq .
```

### 2. Execute o container

```bash
docker run -p 8501:8501 -e GROQ_API_KEY=sua-chave-aqui chat-visao-groq
```

Acesse no navegador: [http://localhost:8501](http://localhost:8501)

## ☁️ Deploy no Render

### 1. Faça push do código para um repositório no GitHub

### 2. No [Render Dashboard](https://dashboard.render.com):

1. Clique em **"New +"** → **"Web Service"**
2. Conecte seu repositório do GitHub
3. Configure:
   - **Name:** `chat-visao-groq` (ou nome desejado)
   - **Runtime:** `Docker`
   - **Branch:** `main`
   - **Health Check Path:** deixe em branco
4. Em **Environment Variables**, adicione:
   - `GROQ_API_KEY` = sua chave da API Groq
5. Clique em **"Create Web Service"**

> ⚠️ O Render detectará automaticamente o Dockerfile e usará a porta definida pela variável de ambiente `$PORT`.

## 📁 Estrutura do Projeto

```
📁 NMday/
├── app.py              # Código principal da aplicação Streamlit
├── requirements.txt    # Dependências Python
├── Dockerfile          # Configuração Docker para Render
└── README.md           # Este arquivo
```

## 🔧 Tecnologias Utilizadas

- **[Streamlit](https://streamlit.io/)** — Interface web
- **[Groq API](https://console.groq.com)** — Modelo de visão Llama 3.2 Vision
- **[Pillow](https://python-pillow.org/)** — Processamento de imagens
- **[Docker](https://www.docker.com/)** — Containerização

## 📄 Licença

Este projeto é de uso livre para fins educacionais e pessoais.
