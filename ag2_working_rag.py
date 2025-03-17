from chromadb import Documents, EmbeddingFunction, Embeddings
from openai import OpenAI
from autogen import AssistantAgent, UserProxyAgent, register_function
import streamlit as st
from autogen_ext.models.openai import AzureOpenAIChatCompletionClient
import asyncio
from dotenv import load_dotenv
load_dotenv()
import os
import autogen
from autogen.agentchat.contrib.retrieve_user_proxy_agent import RetrieveUserProxyAgent

# The secrets in Env
Token = os.environ.get("OPEN_AI_API")
End_point = os.environ.get("AZURE_OPENAI_ENDPOINT")

st.title("AG2 With Custom Loaders : RAGentic BETA Prototype")

llm_config = {
    "model": "gpt-4o-mini",
    "api_key": Token,
}

import chromadb
chromadb.api.client.SharedSystemClient.clear_system_cache()

# Initialize chat history and message in session state
if "chat_history" not in st.session_state:
    st.session_state.chat_history = []

if "message" not in st.session_state:
    st.session_state.message = []

if "docs_path" not in st.session_state:
    st.session_state.docs_path = []

# Clear chat history
if st.button("Clear Chat History"):
    st.session_state.chat_history = []

# Display chat history
for chat in st.session_state.chat_history:
    st.markdown(f"**{chat['sender']}:** {chat['message']}")

class TrackableAssistantAgent(AssistantAgent):
    def _process_received_message(self, message, sender, silent):
        if message:
            if any(key in message for key in ["content"]):
                with st.chat_message(sender.name, avatar="🤖"):
                    for key in message:
                        if key == "content":
                            st.markdown(message[key])
                            st.session_state.chat_history.append({"sender": sender.name, "message": message[key]})
            else:
                with st.chat_message(sender.name, avatar="😊"):
                    st.markdown(message)
                    st.session_state.chat_history.append({"sender": sender.name, "message": message})
        return super()._process_received_message(message, sender, silent)

class TrackableUserProxyAgent(RetrieveUserProxyAgent):
    def _process_received_message(self, message, sender, silent):
        with st.chat_message(sender.name, avatar="😊"):
            st.write(message)
            st.session_state.message.append({"sender": sender.name, "message": message})
        return super()._process_received_message(message, sender, silent)

    is_termination_msg = lambda x: x.get("content", "").rstrip().endswith("TERMINATE")

assistant = TrackableAssistantAgent(
    name="assistant",
    llm_config=llm_config,
    system_message="""
    You should check the context of the question and provide a relevant answer.
    You should always use English to answer
    You MUST reply with TERMINATE after your answer.
    """,
    human_input_mode="NEVER", 
    code_execution_config={"work_dir": "coding", "use_docker": False}
)

class MyEmbeddingFunction(EmbeddingFunction):
    def __init__(self):
        self.client = OpenAI(
            api_key=Token,
        )
        self.deployment_id = os.environ.get("AZURE_EMBEDDING_NAME")

    def split_into_chunks(self, document: str, chunk_size: int = 512) -> list:
        # Split the document into chunks of the specified size
        return [document[i:i + chunk_size] for i in range(0, len(document), chunk_size)]

    def __call__(self, input: Documents) -> Embeddings:
        all_embeddings = []
        for doc in input:
            chunks = self.split_into_chunks(doc)
            for chunk in chunks:
                response = self.client.embeddings.create(
                    input=[chunk],
                    model="text-embedding-3-small"
                ).data[0].embedding
                all_embeddings.append(response)
        return all_embeddings

with st.sidebar:
    st.markdown("### Upload your document")
    uploaded_file = st.file_uploader("Choose a file")
    
    def upload_file(uploaded_file):
        if uploaded_file is not None:
            with open(uploaded_file.name, "wb") as f:
                f.write(uploaded_file.getbuffer())
            st.success("File uploaded successfully")
            file_path = uploaded_file.name
            return file_path


    st.markdown("### Add a link")
    link = st.text_input("Enter a link")
    
    def upload_link(link):
        if link:
            st.success("Link added successfully")
            return link
        return None

    file_content = upload_file(uploaded_file)
    link_content = upload_link(link)

    if st.button("Clear Documents"):
        st.session_state.docs_path = []

    if file_content:
        st.session_state.docs_path.append(file_content)
    if link_content:
        st.session_state.docs_path.append(link_content)

ragproxyagent = TrackableUserProxyAgent(
    name="ragproxyagent",
    human_input_mode="NEVER",
    is_termination_msg=lambda x: x.get("content", "").rstrip().endswith("TERMINATE"),
    retrieve_config={
        "task": "default",
        "docs_path": st.session_state.docs_path,
        "embedding_function": MyEmbeddingFunction(),
        "get_or_create": True, 
        "overwrite": True
    },
    system_message="Send one question only",
)

user_input = st.chat_input("Type your query.")
if user_input:
    st.session_state.chat_history.append({"sender": "user", "message": user_input})
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)

    async def initiate_chat():
        result = None
        result = await ragproxyagent.a_initiate_chat(
            assistant,
            message=ragproxyagent.message_generator,
            problem=user_input,
            max_turns=2, 
            user_input=False, 
            summary_method="reflection_with_llm")
        return result

    result = loop.run_until_complete(initiate_chat())

# Ensure all documents are deleted from context when the app is closed
def on_close():
    st.session_state.docs_path = []
