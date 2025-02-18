from autogen import AssistantAgent, UserProxyAgent, config_list_from_json, GroupChat, GroupChatManager
from dotenv import load_dotenv
load_dotenv()
import os

gpt4o_config = {
    "model": "gpt-4o",
    "api_key": os.environ.get("api_key"),
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
    Wrap the code in a code block that specifies the script type. The user can't modify your code. Don't include multiple code blocks in one response. \
    Do not ask others to copy and paste the result. Check the execution result returned by the executor. \
    If the result indicates there is an error, fix the error and output the code again. Suggest the full code instead of partial code or code changes.\
    If the error can't be fixed or if the task is not solved even after the code is executed successfully, analyse the problem."""
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
  system_message='Research Report Writer. Write a research report based on the findings from the papers categorized by the scientist and exchange with critic to improve \
  the quality of the report.\
  The report should include the following sections: Introduction, Literature Review, Methodology, Results, Conclusion, and References.\
  The report should be written in a clear and concise manner. Make sure to include proper citation and references.',
  llm_config=gpt4o_config
)

groupchat = GroupChat(
  agents=[user_proxy, planner, engineer, scientist, executor, critic, research_report_writer],
  messages=[],
  max_round=50
)

manager = GroupChatManager(groupchat=groupchat, llm_config=gpt4o_config)

output_report = user_proxy.initiate_chat(manager, message = "Write a 4 paragraph research report about how to use mean reversion strategy in trading.")