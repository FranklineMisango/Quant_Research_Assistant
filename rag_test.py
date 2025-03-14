import asyncio
from autogen import AssistantAgent, UserProxyAgent, GroupChat, GroupChatManager
from dotenv import load_dotenv
load_dotenv()
import os
import nest_asyncio
from autogen.agents.experimental import DeepResearchAgent
import autogen
nest_asyncio.apply()
from typing_extensions import Annotated
from autogen.agentchat.contrib.retrieve_user_proxy_agent import RetrieveUserProxyAgent
from typing_extensions import Annotated

config_list = autogen.config_list_from_json("OAI_CONFIG_LIST.json")
assistant = AssistantAgent(
    name="assistant",
    system_message="You are a helpful assistant.",
    llm_config={
        "timeout": 600,
        "cache_seed": 42,
        "config_list": config_list,
    },
)
ragproxyagent = RetrieveUserProxyAgent(
    name="ragproxyagent",
    human_input_mode="NEVER",
    max_consecutive_auto_reply=3,
    retrieve_config={
        "task": "code",
        "docs_path": [
            "Financial_Documents/Market_Data_CSV/3_year_NVDA_Test_data.csv",
        ],
        "chunk_token_size": 2000,
        "model": config_list[0]["model"],
        "vector_db": "chroma",
        "overwrite": True,  # set to True if you want to overwrite an existing collection
        "get_or_create": True,  # set to True to create the collection if it does not exist
    },
    code_execution_config=False,  # set to False if you don't want to execute the code
)

# reset the assistant. Always reset the assistant before starting a new conversation.
assistant.reset()

# given a problem, we use the ragproxyagent to generate a prompt to be sent to the assistant as the initial message.
# the assistant receives the message and generates a response. The response will be sent back to the ragproxyagent for processing.
# The conversation continues until the termination condition is met, in RetrieveChat, the termination condition when no human-in-loop is no code block detected.
# With human-in-loop, the conversation will continue until the user says "exit".
code_problem = "This is 3 years worth of NVDA Data. Act as a financial analyst and provide a long analysis of the data."
chat_result = ragproxyagent.initiate_chat(
    assistant, message=ragproxyagent.message_generator, 
    
problem=code_problem, max_turns=2, user_input=False, summary_method="reflection_with_llm")
print(chat_result.summary)