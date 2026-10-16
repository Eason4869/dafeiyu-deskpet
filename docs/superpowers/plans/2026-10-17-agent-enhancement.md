# DeskPet Agent Enhancement Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline, task by task. The user explicitly authorized implementation after planning; no additional plan approval is required. A fresh whole-branch reviewer checks the result before delivery.

**Goal:** Deliver reliable file/code work, controlled command verification, recoverable task state, and targeted native task/command UI preserving the existing layout.
**Architecture:** Keep the C# model loop and existing UI/session entry points. Extract bounded tool contracts, file enhancements, Windows command lifecycle, encrypted task storage and a workbench view; existing file defenses remain canonical.
**Tech Stack:** WPF/.NET Framework 4.8, shared .NET 10 C#, Win32 Job Objects, existing encrypted JSON storage; no external Agent runtime.
**Spec:** ../specs/2026-10-17-agent-enhancement-design.md

## Global Constraints
- Plan mode blocks writes, directory side effects and project commands at schema and execution levels.
- No new runtime EXE; preserve legacy updater package allowlists. Do not publish or alter the installed app.
- Commands require explicit project-bound configuration; model receives only command IDs. No working-directory sandbox claim.
- Use the current user-authorized project and existing OpenAI/Anthropic credentials without logging secrets.
- Preserve ordinary chat, queue ordering, voice and pet interactions.

## Review Focus
- A save warning from one persona must survive another persona's successful save/load.
- External file edits between read and write must not be overwritten or undone during recovery.
- Cancellation must stop descendants, drain output, persist terminal state and keep updates blocked until finished.
- Untrusted repository files cannot enable commands or alter command recipes.
- Long tool/attachment contexts retain protocol-valid tool pairs and current user input.

### Task 1: Reliability regressions
**Files:** HistoryStore.cs, ConversationSession.cs, PetBusiness.cs, Build.ps1; tests/AgentReliabilityTests.cs.
**Interfaces:** Existing HistoryStore.Load/SaveAsync and ConversationSession.SelectPersona remain compatible; context selection gains a byte/attachment budget helper.
- [ ] Add deterministic failure/switch and cross-persona warning tests, observe failures.
- [ ] Load history outside the session gate; warnings keyed by history path.
- [ ] Budget current attachments plus complete previous turns before transport.
- [ ] Add UI Automation assemblies for AttachmentPickerTests only.
- [ ] Run AgentReliabilityTests, HistoryTests, AgentSessionTests, QueueTests; commit.

### Task 2: Tool registry and project discovery
**Files:** AgentWorkspace.cs (partial), AgentToolCatalog.cs, AgentFileTools.cs; tests/AgentFileToolsTests.cs; Build.ps1.
**Interfaces:** Tool definitions hold name/description/properties/required/read-only. Existing ExecuteAsync(string,string,CancellationToken) remains compatible. New tool output carries truncation/cursor metadata without changing old text contracts.
- [ ] Test directory pagination beyond 200 entries, pattern find, batch read, ignored build trees, traversal/link refusal and plan-mode schemas; observe failures.
- [ ] Centralize schema registration and extend tools using canonical Resolve/ReadBytes/LockDirectories.
- [ ] Run new tests and existing AgentWorkspaceTests/AgentProtocolTests; commit.

### Task 3: Conflict-aware multi-file changes
**Files:** AgentChanges.cs, AgentFileTools.cs, AgentWorkspace.cs; tests/AgentChangesTests.cs.
**Interfaces:** AgentChange tracks path/before/after/backup/status; workspace exposes read-only change snapshots and conflict-checked RestoreAsync.
- [ ] Test two-file patch, failed preflight, ambiguous text, external edit conflict, backup provenance and recovery conflict; observe failures.
- [ ] Track hashes on reads; prevalidate all patch entries; atomically write and record per-file outcomes.
- [ ] Run new and existing workspace tests; commit.

### Task 4: Project-bound command execution
**Files:** AgentCommand.cs, WindowsCommandRunner.cs, Settings.cs, WorkSettingsPanel.cs, AgentWorkspace.cs; tests/AgentCommandTests.cs.
**Interfaces:** AgentCommand {Id,Executable,Arguments,Workspace}; CommandResult {ExitCode,Output,Truncated,TimedOut,ElapsedMilliseconds}; RunAsync(command,timeout,token).
- [ ] Add real fixture process modes for success/failure, output flood, waiting and child creation; prove unknown/disabled/plan commands rejected.
- [ ] Implement suspended launch, Job assignment, output drains, timeout/cancel and descendant cleanup; fail closed on Job errors.
- [ ] Persist opt-in command recipes in settings and provide native editing UI with actual project binding.
- [ ] Run command lifecycle tests in normal Windows permissions and settings tests; commit.

### Task 5: Task state, context checkpoints and execution wiring
**Files:** AgentTaskState.cs, AgentTaskStore.cs, NativeAgentClient.cs, ConversationSession.cs, ConversationQueue.cs; tests/AgentTaskTests.cs; existing protocol fixtures.
**Interfaces:** AgentTaskState snapshots plan/changes/command outcomes by run/persona/workspace; encrypted Save/Load; session exposes CurrentAgentTask and task-change events. Command/file tools feed the same state.
- [ ] Test task isolation, encrypted restart, interrupted operation reconciliation, repeated-error guard and current-input/context preservation; observe failures.
- [ ] Bind run settings, publish truthful tool results, save checkpoints and expose explicit continue without automatic side-effect replay.
- [ ] Wire run_command/read_output/update_plan into both native protocols, enforce byte budgets, compact old outputs as checkpoints without breaking tool groups.
- [ ] Regress stop, queue and update readiness, then commit.

### Task 6: Additive task UI and visual QA
**Files:** AgentWorkbenchPanel.cs, ChatWindow.cs, ChatMessageView.cs, ConversationComposer.cs, WorkSettingsPanel.cs; tests/AgentWorkbenchSmoke.cs.
**Interfaces:** Workbench consumes session task snapshots and existing shared queue; it never starts work merely from navigation or loading state.
- [ ] Add UI smoke for task list, changes, command exit/result, explicit continuation and narrow layout; observe missing UI behavior.
- [ ] Add collapsible task/change/command detail panels and configuration using existing Ui/ChatUi; do not redesign the window, sidebar, palette or composer.
- [ ] Capture and inspect existing 700px/520px windows and actual tool/command results, correct overflow/focus issues.
- [ ] Run ConversationUiSmoke, ComposerSmoke, WorkSettingsTests and new smoke; commit.

### Task 7: Integration, docs and delivery
**Files:** Build.ps1, desktop/DeskPet.Core/DeskPet.Core.csproj, .github/workflows/check.yml, docs/conversation-agent.md, docs/native-agent-design.md, docs/verification.md.
- [ ] Register all tests and shared files; add targeted command fixtures to CI; avoid adding desktop-only tests to headless CI.
- [ ] Run the full Windows CI test list plus new tests, application build, packaging and available portable tests; inspect every failure.
- [ ] Document real user workflow, command authority/output limits and recovery behavior; record pinned references and license-safe independent implementation.
- [ ] Request fresh code review; fix important findings with regressions, rerun affected suites, commit and provide build/screenshots/plan links. No push, release or installed-app replacement is authorized.

### Task 8: Maximized workspace (user scope addition)
**Files:** ChatWindow.cs, AgentTaskPanel.cs, AgentWorkspaceNavigation.cs; AgentWorkbenchSmoke.cs.
- Keep 700px/520px normal window unchanged. On WindowState.Maximized show left project/current-task navigation and right task/change/command inspector, reuse live controls and central conversation/composer.
- Restore hides both side columns and moves same task panel back, keeping draft, focus, selected history and expand state.
- RED then GREEN smoke verifies actual maximize/restore, visible sidebar, same task controls and retained draft; capture and inspect both layouts. Existing ConversationUiSmoke/ComposerSmoke must remain green.
