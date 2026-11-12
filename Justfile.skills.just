# Agent-skill recipes. Imported by the Justfile; every recipe names one Purview.BuildSdk target, which runs
# once for the whole solution. See AGENTS.md ("Installed skills") for the sources and the universal-only rule.

skills_project := solution

# Update agent skills: re-add every skills-lock.json source, refreshing installed skills and adding new ones
[group('Agent skills')]
skills-update *args:
    dotnet msbuild "{{ skills_project }}" "-t:UpdatePurviewSkills;FixPurviewSkills" -v:n {{ args }}

# Check that only the skill directories of the agents in PurviewSkillsAgents exist
[group('Agent skills')]
skills-check *args:
    dotnet msbuild "{{ skills_project }}" -t:CheckPurviewSkills -v:n {{ args }}

# Remove the skills CLI's links from agent directories PurviewSkillsAgents does not select
[group('Agent skills')]
skills-fix *args:
    dotnet msbuild "{{ skills_project }}" -t:FixPurviewSkills -v:n {{ args }}
