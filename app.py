import asyncio
from autogen import AssistantAgent, UserProxyAgent, GroupChat, GroupChatManager
from dotenv import load_dotenv
from autogen.agents.experimental import  DeepResearchAgent
load_dotenv()
import os
import nest_asyncio
nest_asyncio.apply()

gpt4o_config = {
    "model": "gpt-4o",
    "api_key": os.environ.get("OPEN_AI_API"),
}

user_proxy = UserProxyAgent(
  name="Admin",
  system_message="A human admin. Interact with a planner to discuss the plan of execution. This plan needs to be approved by this admin.",
  code_execution_config=False
)

planner = AssistantAgent(
  name='Planner',
  system_message='Planner. Suggest a plan. Revise the plan based on feedback from a critic agent.\
    The plan may involve an engineer who can write code and a scientist who doesn’t write code. \
    Explain the plan first. Be clear which step is performed by an engineer, and which step is performed by a scientist.',
  llm_config=gpt4o_config,
)

engineer = AssistantAgent(
  name="Engineer",
  llm_config=gpt4o_config,
  system_message="""Engineer. You follow an approved plan. You write Python/shell code to solve tasks.\
    Wrap the code in a code block that specifies the script type. The user can't modify your code. Don't include multiple code blocks in one response.\
    Do not ask others to copy and paste the result. Check the execution result returned by the executor.\
    If the result indicates there is an error, fix the error and output the code again. Suggest the full code instead of partial code or code changes.\
    If the error can't be fixed or if the task is not solved even after the code is executed successfully, analyse the problem.
    For Graph generation, use matplotlib/sns and make sure there are buy/sell signals as cones and coloring to indicate bear/bull trends .\
    For PDF generation, There should be no huge whitespaces between texts. Make it compact and readable\with all images, code used, tables and graphss included.',


    """
)

scientist = AssistantAgent(
  name="Scientist",
  llm_config=gpt4o_config,
  system_message="""Scientist. You follow an approved plan. You are able to categorize papers after seeing their abstracts printed. You don't write code.\
    you provided detailed resource  reports for the ResearchWriter to write comprehensive research reports."""
)

executor = UserProxyAgent(
  name="Executor",
  system_message="Executor. Execute the code written by the engineer and report the result.",
  human_input_mode="NEVER",
  code_execution_config={
      "last_n_messages": 3,
      "work_dir": "paper",
      "use_docker": True,
  }
  # Please set use_docker=True if docker is available to run the generated code. Using docker is safer than running the generated code directly.
)

critic = AssistantAgent(
  name="Critic",
  system_message="Critic. Double check, claims, code and report from other agents and provide feedback. \
  Check whether the final research report includes adding verifiable info such as source ",
  llm_config=gpt4o_config,
)

research_report_writer = AssistantAgent(
  name='ResearchWriter',
  system_message='Research Report Writer. Write a research report based on the findings from the papers categorized by the scientist and exchange with critic to improve the quality of the report.\
  The report should include the following sections: Title, Introduction, Literature Review, Methodology, Results, Conclusion, and References.\
  The report should have a subtitle with :  Produced by Frankline&CoLP Quant Research AI Assstant.\
  The report should be written in a clear and concise manner. Make sure to include proper citation and references.\
  Ask the Engineer to generate graphs and tables for the report. The report should be saved as a PDF file. \
  The engineer can run code to save the pdf file. The report should be saved as a PDF file.',
  llm_config=gpt4o_config
)

groupchat = GroupChat(
  agents=[user_proxy, planner, engineer, scientist, executor, critic, research_report_writer],
  messages=[],
  max_round=50
)

manager = GroupChatManager(groupchat=groupchat, llm_config=gpt4o_config)

#output_report = user_proxy.initiate_chat(manager, message = "Can you read this book 'https://download.e-bookshelf.de/download/0000/5680/29/L-G-0000568029-0002381765.pdf' and tell me what strategies are listed?")

deep_research_llm_config = {
    "config_list": [{"api_type": "openai", "model": "gpt-4o", "api_key": os.environ["OPEN_AI_API"]}],
}
agent = DeepResearchAgent(
    name="DeepResearchAgent",
    llm_config=deep_research_llm_config,
)

message = "Create a report explaining how FPGA can be used in the field of AI and ML. Include the following sections: Title, Introduction, Literature Review, Methodology, Results, Conclusion, and References. The report should be written in a clear and concise manner. Make sure to include proper citation and references. Ask the Engineer to generate graphs and tables for the report. The report should be saved as a PDF file."

async def main():
    result = agent.run(
        message=message,
        tools=agent.tools,
        max_turns=2,
        user_input=False,
        summary_method="reflection_with_llm",
    )
    print(result.summary)

if __name__ == "__main__":
    asyncio.run(main())