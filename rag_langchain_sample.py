import asyncio
import pandas as pd
from langchain_experimental.tools.python.tool import PythonAstREPLTool
from autogen_ext.tools.langchain import LangChainToolAdapter
from autogen_ext.models.openai import OpenAIChatCompletionClient
from autogen_agentchat.messages import TextMessage
from autogen_agentchat.agents import AssistantAgent
from autogen_agentchat.ui import Console
from autogen_core import CancellationToken


async def main() -> None:
    df = pd.read_csv("https://raw.githubusercontent.com/pandas-dev/pandas/main/doc/data/titanic.csv")  # type: ignore
    tool = LangChainToolAdapter(PythonAstREPLTool(locals={"df": df}))
    model_client = OpenAIChatCompletionClient(model="gpt-4o")
    agent = AssistantAgent(
        "assistant",
        tools=[tool],
        model_client=model_client,
        system_message="Use the `df` variable to access the dataset.",
    )
    await Console(
        agent.on_messages_stream(
            [TextMessage(content="What's the average age of the passengers?", source="user")], CancellationToken()
        )
    )


asyncio.run(main())