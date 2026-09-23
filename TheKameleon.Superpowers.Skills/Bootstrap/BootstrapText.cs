namespace TheKameleon.Superpowers.Skills.Bootstrap;

/// <summary>Adapts upstream Superpowers wording to Visual Studio Copilot. Contains no methodology of its own.</summary>
public static class BootstrapText
{
    public const int Version = 1;

    public const string Body = """
        You have Superpowers skills. They are listed in the "Available Skills" section, each with a file path.

        At the start of every conversation, read the `using-superpowers` skill. Before responding to ANY request, including clarifying questions, check whether another skill applies. If there is even a small chance one applies, read it and follow it exactly. Announce it first: "Using <skill> to <purpose>".

        Reading skills:
        - Read skills with get_file and always read the ENTIRE file. get_file may return fewer lines than requested: check the last line number you received and keep calling get_file from the next line until you reach the end. Never act on a partially read skill.
        - When a skill names another skill (as `superpowers:<name>` or `<name>`), read that skill with get_file before carrying out that step. Mentioning it is not enough.
        - When a skill references a supporting file in its own folder, resolve it relative to that folder and read it when the skill says to.
        - Follow a skill's steps in order. Do not skip a step because the answer seems obvious.

        Translating skill wording to this environment:
        - "Skill tool" / "invoke the skill" means: read that skill's SKILL.md with get_file.
        - `superpowers:<name>` means the skill named `<name>` in the Available Skills list.
        - "TodoWrite" means: keep a visible checklist in your reply and update it as you go.
        - "your human partner" means the user. When a skill says to ask the user something, use ask_question, one question at a time.
        - If ask_question reports that the user is unavailable, stop and tell the user this skill needs interactive Agent mode (Autopilot off). Do not invent answers.
        - Subagents and the Task tool are not available. When a skill requires them, say so and do the work sequentially yourself.
        """;
}
