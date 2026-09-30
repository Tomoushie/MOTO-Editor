# Couverture du catalogue de réglages

> **Généré automatiquement — analyse seule, aucun fichier modifié.**
> Source : `scripts/settings-coverage.ps1` · Périmètre : 629 fichiers .cs réellement compilés

## Chiffres

| Mesure | Valeur |
|---|---|
| Réglages DÉCLARÉS au catalogue | 332 |
| Clés lues par du code compilé | 72 |
| **Déclarés ET lus → réellement opérants** | **45** |
| Déclarés mais INERTES | 287 |
| **Part réellement opérante** | **13.6 %** |

## Réglages réellement opérants

| Clé | Catégorie | Lue par |
|---|---|---|
| `agent_engine` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs |
| `agent_max_minutes` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs |
| `agent_max_steps` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs |
| `agent_model` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs |
| `agent_num_ctx` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs |
| `agent_thought` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs |
| `agent_tool_mode` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs |
| `agent_verify_command` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs |
| `buffer_font_size` | Apparence | Moto.Editor\Settings\SettingsApplier.cs |
| `context_engine_enabled` | Agent | Moto.Core\Moto.AI\Context\ContextEngine.cs |
| `doc_auto_update` | Agent | Moto.Core\Doc\DocEngine.cs |
| `doc_on_project_open` | Agent | Moto.Editor\MainPage.Panels.cs |
| `lsp_diagnostics` | Langages & Outils | Moto.Editor\Settings\SettingsApplier.cs |
| `minimap_show` | Éditeur | Moto.Editor\Settings\SettingsApplier.cs |
| `ollama_endpoint` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs, Moto.Core\Moto.AI\Generation\GenerationSettings.cs, Moto.Core\Moto.AI\Internal\OllamaClient.cs, Moto.Editor\Pages\AiSettingsPage.xaml.cs |
| `ollama_model` | IA Locale | Moto.Core\Moto.AI\Autonomy\V2\AgentV2Settings.cs, Moto.Core\Moto.AI\Generation\GenerationSettings.cs, Moto.Core\Moto.AI\Internal\OllamaClient.cs, Moto.Editor\Pages\AiSettingsPage.xaml.cs |
| `ollama_timeout_seconds` | IA Locale | Moto.Core\Moto.AI\Internal\OllamaClient.cs |
| `platform_auto_detect` | Agent | Moto.Editor\MainPage.Panels.cs |
| `power_mode` | Agent | Moto.Core\Performance\PerformanceEngine.cs, Moto.Editor\Controls\AiComposerBarView.xaml.cs |
| `pp_auto_reveal` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_dock` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_entry_spacing` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_file_icons` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_folder_icons` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_git_indicator` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_git_status` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_hide_gitignore` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_hide_hidden` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_horizontal_scroll` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_indent` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `pp_width` | Panneaux | Moto.Editor\Settings\PanelSettings.cs |
| `tabs_activate_on_close` | Fenêtre & Layout | Moto.Editor\ViewModels\MainViewModel.cs |
| `tabs_bar_buttons` | Fenêtre & Layout | Moto.Editor\Controls\EditorPaneView.xaml.cs |
| `tabs_close_position` | Fenêtre & Layout | Moto.Editor\Settings\TabBarSettings.cs |
| `tabs_file_icons` | Fenêtre & Layout | Moto.Editor\Settings\TabBarSettings.cs |
| `tabs_max` | Fenêtre & Layout | Moto.Editor\ViewModels\MainViewModel.cs |
| `tabs_nav_buttons` | Fenêtre & Layout | Moto.Editor\Controls\EditorPaneView.xaml.cs |
| `tabs_show` | Fenêtre & Layout | Moto.Editor\Controls\EditorPaneView.xaml.cs |
| `tabs_show_close` | Fenêtre & Layout | Moto.Editor\Settings\TabBarSettings.cs |
| `tabs_show_diagnostics` | Fenêtre & Layout | Moto.Editor\Settings\TabBarSettings.cs |
| `tb_branch_name` | Fenêtre & Layout | Moto.Editor\Settings\TitleBarSettings.cs |
| `tb_button_layout` | Fenêtre & Layout | Moto.Editor\Settings\TitleBarSettings.cs |
| `tb_menus` | Fenêtre & Layout | Moto.Editor\Settings\TitleBarSettings.cs |
| `tb_project_items` | Fenêtre & Layout | Moto.Editor\Settings\TitleBarSettings.cs |
| `theme_mode` | Apparence | Moto.Editor\Settings\SettingsApplier.cs |

## Réglages INERTES, par catégorie

Ce sont les réglages affichés dans la fenêtre Réglages dont AUCUN code
compilé ne lit la clé : ils sont persistés, mais sans effet. C'est la
matière première du palier « tout ce qui est annoncé fonctionne ».

### Fenêtre & Layout — 37 réglage(s) inerte(s)

- `border_size`
- `bottom_dock_layout`
- `centered_left_padding`
- `centered_right_padding`
- `focus_follows_debounce`
- `focus_follows_mouse`
- `fullscreen_mode`
- `horizontal_split_direction`
- `inactive_opacity`
- `preview_code_nav`
- `preview_enabled`
- `preview_file_finder`
- `preview_keep_on_nav`
- `preview_multibuffer`
- `preview_project_panel`
- `sb_active_file`
- `sb_cursor_position`
- `sb_debugger`
- `sb_diagnostics`
- `sb_encoding`
- `sb_language`
- `sb_line_endings`
- `sb_project_panel`
- `sb_search`
- `sb_terminal`
- `tabs_git_status`
- `tabs_pinned_layout`
- `tb_branch_icon`
- `tb_onboarding`
- `tb_sign_in`
- `tb_user_menu`
- `tb_user_picture`
- `tb_worktree`
- `use_system_window_tabs`
- `vertical_split_direction`
- `window_decorations`
- `zoomed_padding`

### Panneaux — 32 réglage(s) inerte(s)

- `ap_button`
- `ap_dock`
- `ap_flexible`
- `ap_height`
- `ap_limit_width`
- `ap_max_width`
- `ap_width`
- `cp_button`
- `cp_dock`
- `cp_width`
- `dp_dock`
- `gp_button`
- `gp_click_behavior`
- `gp_collapse_untracked`
- `gp_commit_max_len`
- `gp_count_badge`
- `gp_diff_stats`
- `gp_dock`
- `gp_fallback_branch`
- `gp_group`
- `gp_scrollbar`
- `gp_sort`
- `gp_starts_open`
- `gp_status_style`
- `gp_tree_view`
- `gp_width`
- `op_auto_fold`
- `op_auto_reveal`
- `op_button`
- `op_dock`
- `op_indent_guides`
- `pp_count_badge`

### AI — 31 réglage(s) inerte(s)

- `ai_disabled`
- `ai.agents.enabled`
- `ai.agents.explainability`
- `ai.circuit.threshold`
- `ai.prefetch.adaptive`
- `ai.profiles.checklist`
- `ai.profiles.minimalist`
- `ai.profiles.pedagogy`
- `ai.profiles.timeline`
- `ai.profiles.tutorial`
- `ai.speculative.enabled`
- `ai.telemetry.enabled`
- `auto_compact_threshold`
- `cancel_on_terminal_stop`
- `enable_feedback`
- `ep_data_collection`
- `ep_disable_language_scope`
- `ep_display_mode`
- `expand_edit_card`
- `expand_terminal_card`
- `message_editor_min_lines`
- `notify_agent_waiting`
- `play_sound_agent_done`
- `show_edit_predictions`
- `show_merge_conflict`
- `show_turn_stats`
- `single_file_review`
- `terminal_thread_init_cmd`
- `thinking_display`
- `threads_sidebar_side`
- `use_modifier_to_send`

### Agent — 28 réglage(s) inerte(s)

- `ai_cache_enabled`
- `auto_compact`
- `auto_doc`
- `autolink_auto_apply`
- `autolink_enabled`
- `autolink_scan_interval_sec`
- `context_auto_apply`
- `context_scan_interval_sec`
- `context_show_low_priority`
- `default_model`
- `doc_folder`
- `doc_include_private`
- `evolution_enabled`
- `evolution_interval_min`
- `max_tokens`
- `performance_full_auto`
- `performance_show_indicator`
- `platform_auto_validate`
- `platform_avalonia_linux`
- `platform_ci_provider`
- `platform_generate_ci`
- `platform_include_linux`
- `platform_incremental_validate`
- `platform_smart_detect`
- `prefer_internal`
- `story_comments`
- `temperature`
- `thread_persistence`

### Éditeur — 25 réglage(s) inerte(s)

- `auto_indent`
- `auto_save`
- `auto_save_delay`
- `autoclose_brackets`
- `autoclose_quotes`
- `completions_enabled`
- `fetch_timeout`
- `format_on_save`
- `hard_tabs`
- `hover_popover`
- `inlay_hints`
- `line_numbers`
- `minimap_max_width`
- `preferred_line_length`
- `relative_line_numbers`
- `remove_trailing_whitespace`
- `scroll_beyond_last_line`
- `scrollbar_diagnostics`
- `scrollbar_show`
- `show_gutter`
- `show_whitespace`
- `signature_help`
- `soft_wrap`
- `tab_size`
- `vertical_scroll_margin`

### Terminal — 22 réglage(s) inerte(s)

- `terminal_alternate_scroll`
- `terminal_audible_bell`
- `terminal_breadcrumbs`
- `terminal_copy_on_select`
- `terminal_cursor_blinking`
- `terminal_cursor_shape`
- `terminal_default_height`
- `terminal_default_width`
- `terminal_detect_venv`
- `terminal_env_vars`
- `terminal_font_family`
- `terminal_font_size`
- `terminal_font_weight`
- `terminal_keep_selection_on_copy`
- `terminal_max_scroll_lines`
- `terminal_min_contrast`
- `terminal_open_links_mouse`
- `terminal_option_as_meta`
- `terminal_scroll_multiplier`
- `terminal_shell`
- `terminal_show_scrollbar`
- `terminal_working_dir`

### Recherche & Fichiers — 17 réglage(s) inerte(s)

- `close_on_file_delete`
- `file_finder_icons`
- `file_finder_include_ignored`
- `file_finder_skip_focus`
- `file_scan_depth`
- `file_scan_exclusions`
- `file_scan_inclusions`
- `restore_file_state`
- `scan_symbolic_links`
- `search_case_sensitive`
- `search_center_on_match`
- `search_include_ignored`
- `search_regex`
- `search_smartcase`
- `search_whole_word`
- `search_wrap`
- `seed_search_from_cursor`

### Apparence — 17 réglage(s) inerte(s)

- `agent_font_size`
- `buffer_font_family`
- `buffer_font_weight`
- `code_fade`
- `current_line_highlight`
- `cursor_blink`
- `cursor_shape`
- `dark_theme`
- `indent_guides`
- `light_theme`
- `line_height`
- `reduce_motion`
- `rounded_selection`
- `selection_highlight`
- `ui_font_family`
- `ui_font_size`
- `wrap_guides`

### Version Control — 17 réglage(s) inerte(s)

- `git_blame_avatar`
- `git_blame_commit_summary`
- `git_blame_delay`
- `git_blame_enabled`
- `git_blame_location`
- `git_blame_min_column`
- `git_blame_padding`
- `git_branch_author`
- `git_diff_base`
- `git_diff_full_file`
- `git_gutter_debounce`
- `git_gutter_visibility`
- `git_hunk_style`
- `git_integration`
- `git_path_style`
- `git_stage_restore_buttons`
- `git.enabled`

### Général — 14 réglage(s) inerte(s)

- `accessible_mode`
- `auto_update`
- `close_no_tabs`
- `editor.update.channel`
- `last_window_closed`
- `private_files`
- `redact_private`
- `restore_on_startup`
- `restore_unsaved`
- `system_path_prompts`
- `system_prompts`
- `telemetry_diagnostics`
- `telemetry_metrics`
- `trust_all_projects`

### Collaboration — 10 réglage(s) inerte(s)

- `collab_input_device`
- `collab_mute_on_join`
- `collab_output_device`
- `collab_share_on_join`
- `collab.annotations.enabled`
- `collab.pr.enabled`
- `collab.roles.enabled`
- `collab.runconfigs.enabled`
- `collab.scratchpads.enabled`
- `collab.whiteboard.enabled`

### Langages & Outils — 8 réglage(s) inerte(s)

- `file_types`
- `include_warnings`
- `inline_diagnostics`
- `lsp_completions`
- `lsp_enabled`
- `lsp_highlights`
- `max_severity`
- `prettier_allowed`

### Developer — 6 réglage(s) inerte(s)

- `devops.crashtriage.enabled`
- `devops.featureflags.enabled`
- `devops.fuzzing.enabled`
- `devops.journeys.enabled`
- `devops.perfgate.enabled`
- `perf_profiler`

### Marketplace — 6 réglage(s) inerte(s)

- `marketplace.donations.enabled`
- `marketplace.payment.currency`
- `marketplace.sandbox.enabled`
- `marketplace.trial.days`
- `marketplace.trial.enabled`
- `marketplace.vulnscan.auto`

### Débogueur — 5 réglage(s) inerte(s)

- `debugger_timeout`
- `format_dap_logs`
- `log_dap`
- `save_breakpoints`
- `stepping_granularity`

### Débutant — 4 réglage(s) inerte(s)

- `explain_everything`
- `nocode_mode`
- `pair_programming`
- `tutor_mode`

### MCP — 3 réglage(s) inerte(s)

- `mcp.adv.checkpointing`
- `mcp.enabled`
- `mcp.subagents`

### Raccourcis — 3 réglage(s) inerte(s)

- `base_keymap`
- `helix_mode`
- `vim_mode`

### Network — 2 réglage(s) inerte(s)

- `network_proxy`
- `server_url`

## Clés hors catalogue

Utilisées comme **état applicatif** (géométrie de fenêtre, dernier dossier,
jeton GitHub...), pas comme réglages utilisateur. Elles partagent le même
stockage que le catalogue — d'où la confusion possible.

- `active_profile`
- `ai_confirm_mode`
- `ai_local_max_context_tokens`
- `ai_local_max_tokens`
- `ai_local_temperature`
- `ai.default_model`
- `ai.embedded.enableKvCacheCompression`
- `ai.embedded.enableParallelDecoding`
- `ai.embedded.enableQuantizationSwitching`
- `ai.embedded.notifyBenchmark`
- `ai.embedded.notifyDownload`
- `ai.embedded.useMemoryMapping`
- `ai.snippets.enabled`
- `app.firstLaunchCompleted`
- `cortex_mode`
- `dashboard`
- `editor.update.autoCheck`
- `editor.update.releaseUrl`
- `github.token`
- `github.username`
- `me`
- `pending`
- `window.height`
- `window.width`
- `window.x`
- `window.y`
- `workspace.last_folder`

