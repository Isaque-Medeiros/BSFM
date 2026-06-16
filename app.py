"""
Aplicacao Streamlit de Chat com Visao Computacional usando Groq (Llama 3.2 Vision)
Permite upload/colagem de imagens e perguntas sobre elas via chat.
"""

import os
import base64
from io import BytesIO

import streamlit as st
from groq import Groq
from PIL import Image
from dotenv import load_dotenv  # Importe a biblioteca

# Carrega as variaveis do arquivo .env se ele existir
load_dotenv()

# ------------------------------------------------------------------------------
# Configuracao da pagina
# ------------------------------------------------------------------------------
st.set_page_config(
    page_title="Chat com Visao Computacional (Groq)",
    page_icon="🤖",
    layout="wide",
)
# ------------------------------------------------------------------------------
# Inicializacao do cliente Groq
# ------------------------------------------------------------------------------
# ------------------------------------------------------------------------------
# Inicializacao do cliente Groq
# ------------------------------------------------------------------------------
@st.cache_resource
def get_groq_client():
    # O Render vai injetar a variavel do painel diretamente aqui
    api_key = os.environ.get("GROQ_API_KEY")
    
    if not api_key:
        st.error(
            "Variável de ambiente GROQ_API_KEY não encontrada. "
            "Certifique-se de configurá-la no painel do Render."
        )
        st.stop()
    return Groq(api_key=api_key)


client = get_groq_client()
# ------------------------------------------------------------------------------
# Estado da sessaoo
# ------------------------------------------------------------------------------
if "messages" not in st.session_state:
    st.session_state.messages = []

if "uploaded_image_base64" not in st.session_state:
    st.session_state.uploaded_image_base64 = None

if "uploaded_image_bytes" not in st.session_state:
    st.session_state.uploaded_image_bytes = None

# ------------------------------------------------------------------------------
# Funcoes auxiliares
# ------------------------------------------------------------------------------
def encode_image_to_base64(image_bytes: bytes) -> str:
    """Converte bytes de imagem para string Base64."""
    return base64.b64encode(image_bytes).decode("utf-8")


def pil_to_bytes(image: Image.Image, fmt: str = "PNG") -> bytes:
    """Converte um objeto PIL.Image para bytes."""
    buf = BytesIO()
    image.save(buf, format=fmt)
    return buf.getvalue()


def reset_conversation():
    """Limpa o historico de mensagens e a imagem carregada."""
    st.session_state.messages = []
    st.session_state.uploaded_image_base64 = None
    st.session_state.uploaded_image_bytes = None


def build_vision_prompt(user_question: str, image_base64: str) -> list:
    """
    Monta a lista de mensagens no formato esperado pela API do Groq
    para o modelo de visao Llama 3.2 Vision.
    """
    return [
        {
            "role": "user",
            "content": [
                {
                    "type": "image_url",
                    "image_url": {
                        "url": f"data:image/jpeg;base64,{image_base64}"
                    },
                },
                {
                    "type": "text",
                    "text": user_question,
                },
            ],
        }
    ]


def ask_groq_vision(user_question: str, image_base64: str) -> str:
    """
    Envia a pergunta + imagem para o modelo de visao do Groq
    e retorna a resposta textual.
    """
    messages = build_vision_prompt(user_question, image_base64)

    try:
        response = client.chat.completions.create(
            model="llama-3.2-11b-vision-preview",
            messages=messages,
            temperature=0.7,
            max_tokens=1024,
        )

        return response.choices[0].message.content

    except Exception as e:
        error_msg = str(e).lower()

        if "rate limit" in error_msg or "429" in error_msg:
            return (
                "⚠️ **Limite de requisicoes excedido.** "
                "Aguarde alguns instantes e tente novamente."
            )
        elif "timeout" in error_msg:
            return (
                "⚠️ **A requisicao excedeu o tempo limite.** "
                "Tente uma pergunta mais simples ou verifique sua conexao."
            )
        else:
            return f"⚠️ **Erro ao processar a requisicao:** {e}"


# ------------------------------------------------------------------------------
# Sidebar - Upload de imagem
# ------------------------------------------------------------------------------
with st.sidebar:
    st.header("📤 Upload de Imagem")

    uploaded_file = st.file_uploader(
        "Escolha uma imagem (PNG, JPG, JPEG)",
        type=["png", "jpg", "jpeg"],
        label_visibility="collapsed",
    )

    if uploaded_file is not None:
        # Ler bytes da imagem
        image_bytes = uploaded_file.read()
        image = Image.open(BytesIO(image_bytes))

        # Salvar no estado da sessao
        st.session_state.uploaded_image_bytes = image_bytes
        st.session_state.uploaded_image_base64 = encode_image_to_base64(image_bytes)

        # Exibir preview na sidebar
        st.image(image, caption="Imagem carregada", use_container_width=True)
        st.success(f"Imagem carregada: {uploaded_file.name}")

    else:
        st.info("Nenhuma imagem carregada.")
        if st.session_state.uploaded_image_base64 is None:
            st.markdown(
                "💡 **Dica:** Você pode carregar uma imagem ou colar "
                "(Ctrl+V) diretamente no campo de upload acima."
            )

    st.divider()

    if st.button("🗑️ Nova Conversa", use_container_width=True):
        reset_conversation()
        st.rerun()

# ------------------------------------------------------------------------------
# Corpo principal - Chat
# ------------------------------------------------------------------------------
st.title("🤖 Chat com Visão Computacional")
st.markdown(
    "Faça upload de uma imagem na barra lateral e faça perguntas sobre ela. "
    "O modelo **Llama 3.2 Vision (Groq)** responderá com base no conteúdo visual."
)

# Exibir a imagem carregada no chat (se houver)
if st.session_state.uploaded_image_base64 is not None and st.session_state.uploaded_image_bytes is not None:
    image = Image.open(BytesIO(st.session_state.uploaded_image_bytes))
    st.image(image, caption="Imagem atual", use_container_width=True)
    st.divider()

# Exibir historico de mensagens
for msg in st.session_state.messages:
    with st.chat_message(msg["role"]):
        st.markdown(msg["content"])

# ------------------------------------------------------------------------------
# Input do usuario
# ------------------------------------------------------------------------------
user_question = st.chat_input("Digite sua pergunta sobre a imagem...")

if user_question:
    # Validar se ha uma imagem carregada
    if st.session_state.uploaded_image_base64 is None:
        st.warning("⚠️ Por favor, carregue uma imagem antes de fazer uma pergunta.")
    else:
        # Adicionar mensagem do usuario ao historico
        st.session_state.messages.append({"role": "user", "content": user_question})
        with st.chat_message("user"):
            st.markdown(user_question)

        # Obter resposta do Groq
        with st.chat_message("assistant"):
            with st.spinner("🤔 Analisando imagem..."):
                answer = ask_groq_vision(
                    user_question, st.session_state.uploaded_image_base64
                )
            st.markdown(answer)

        # Adicionar resposta ao historico
        st.session_state.messages.append({"role": "assistant", "content": answer})
