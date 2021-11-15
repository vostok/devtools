import requests
import os
import tempfile
import subprocess
import sys

SECRET = "<secret>"

GITHUB_PERSONAL_ACCESS_TOKEN = SECRET
APPVEYOR_BEARER = SECRET

def normalize_repo_name(repo):
	return repo.replace("vostok.", "")

# For Windows
def add_or_override_ci_in_repo(repo, url, workflow_content):
	with tempfile.TemporaryDirectory() as tmp:
		subprocess.call(f'powershell.exe cd {tmp}; git clone {url}', shell=True)
		repo_path = os.path.join(tmp, normalize_repo_name(repo))
		workflow_path = os.path.join(repo_path, ".github", "workflows")
		os.makedirs(workflow_path, exist_ok=True)
		with open(os.path.join(workflow_path, 'ci.yml'), 'w') as f:
			f.write(workflow_content)
		subprocess.call(f'powershell.exe cd {repo_path}; git add .; git commit -m \'Update ci\'; git push', shell=True)

def add_override_ci_in_all_repos(github, repos, github_repos):
	workflow_content = github.get_actual_workflow()
	for i, repo in enumerate(repos):
		print(f"Processing {repo}... ({i + 1} out of {len(repos)})")
		add_or_override_ci_in_repo(repo, github_repos[repo], workflow_content)

def toggle_webhooks_in_all_repos(github, repos, enabled):
	for i, repo in enumerate(repos):
		print(f"Processing {repo}... ({i + 1} out of {len(repos)})")
		webhooks = github.get_webhooks_from(normalize_repo_name(repo))
		for hook, hook_id in webhooks:
			if hook == "web":
				github.toggle_webhook(normalize_repo_name(repo), hook_id, enabled)
			else:
				print(f"Ignoring hook {hook} in {repo}")


# See https://www.appveyor.com/docs/api/
class AppveyorClient:
	def __init__(self, bearer):
		self.bearer = bearer

	def check_secrets(self):
		if self.bearer == SECRET:
			print("Appveyor bearer is not specified!")
			sys.exit(1)

	def parse_repositories(self, data):
		return [repo['repositoryName'].replace('/', '.') for repo in data]

	def get_vostok_repositories(self):
		self.check_secrets()

		url = 'https://ci.appveyor.com/api/account/vostok/projects/'

		headers = {
			'Authorization': f'Bearer {self.bearer}',
			'Content-type': 'application/json'
		}

		return set(self.parse_repositories(requests.get(url=url, headers=headers).json()))


# See https://docs.github.com/en/rest/reference/repos
class GithubClient:
	def __init__(self, token=None):
		self.personal_access_token = token

	def check_secrets(self):
		if not self.personal_access_token or self.personal_access_token == SECRET:
			print("Github personal access token is not specified!")
			sys.exit(1)

	def parse_repositories(self, data):
		return [(repo['full_name'].replace('/', '.'), repo['html_url']) for repo in data]

	def get_vostok_repositories(self):
		url = 'https://api.github.com/orgs/vostok/repos?per_page=100000'

		return {repo: url for repo, url in self.parse_repositories(requests.get(url=url).json())}

	def build_headers(self):
		self.check_secrets()

		return {"Authorization": "token {}".format(self.personal_access_token)}

	def get_webhooks_from(self, repo):
		headers = self.build_headers()

		url = f"https://api.github.com/repos/vostok/{repo}/hooks"

		return [(hook["name"], hook["id"]) for hook in requests.get(url=url, headers=headers).json()]

	def toggle_webhook(self, repo, id, enabled):
		headers = self.build_headers()

		url = f"https://api.github.com/repos/vostok/{repo}/hooks/{id}"

		active_state = "true" if enabled else "false"
		data = f'{{"active": {active_state}}}'

		return requests.patch(url=url, headers=headers, data=data).json()

	def get_actual_workflow(self):
		url = "https://raw.githubusercontent.com/vostok/devtools/master/library-ci/github_ci.yml"

		return requests.get(url=url).text

appveyor = AppveyorClient(APPVEYOR_BEARER)
github = GithubClient(GITHUB_PERSONAL_ACCESS_TOKEN)

appveyor_repos = appveyor.get_vostok_repositories()
github_repos = github.get_vostok_repositories()

def get_intersection_repos():
	repos = []

	for repo in sorted(github_repos.keys()):
		if repo not in appveyor_repos:
			print(f"Skipping {repo} because it is not on appveyor.")
			continue
		repos.append(repo)

	return repos

repos = get_intersection_repos()

print("\n")
print(f"Ready to add/override CI in the following {len(repos)} repos:", end='\n\t')
print("\n\t".join(repos))
print("\n")
print("Please check carefully that all desired repositories are present and uncomment desired operation.")


# add_override_ci_in_all_repos(github, repos, github_repos)
# toggle_webhooks_in_all_repos(github, repos, False)
