extends "res://Tests/Presentation/DraftLayoutCheck.gd"

const MEADOW_BOARD := Vector2(6, 5)
const RECTANGULAR_BOARD := Vector2(7, 5)
const TURN_LIMIT := 2400

func run() -> void:
	root.size = Vector2i(1000, 690)
	var rectangular := spawn("demonstration")
	await settle()
	expect(rectangular.get_node("%PlayerOne").text.begins_with("You") and rectangular.get_node("%PlayerTwo").text.begins_with("CPU"),
		"Normal startup did not default to Versus CPU.")
	expect(rectangular.get_node("%RuleSetBadge").text == "Knight Support", "Normal startup did not default to Knight Support.")
	check_banner(rectangular.get_node("%BoardRenderer"), RECTANGULAR_BOARD, ["0", "0"])
	var controls: Control = rectangular.get_node("Frame/Layout/Controls")
	expect(controls.size.x <= 160, "Landscape controls take too much of the board's width: %s." % controls.size.x)
	await capture("opening-landscape")
	root.size = Vector2i(410, 720)
	await settle()
	check_banner(rectangular.get_node("%BoardRenderer"), RECTANGULAR_BOARD, ["0", "0"])
	expect(controls.size.y <= 130, "Portrait controls take too much of the board's height: %s." % controls.size.y)
	await capture("opening-portrait")
	rectangular.queue_free()
	await process_frame

	var game := spawn("open-meadow", 0)
	# Hold the main-thread pump so the human can highlight a tile before CPU submission.
	game.set_process(false)
	await settle()
	var one: Button = game.get_node("%PlayerOne")
	var two: Button = game.get_node("%PlayerTwo")
	var phase: Label = game.get_node("%Phase")
	var board: Control = game.get_node("%BoardRenderer")
	var submit: Button = game.get_node("%Submit")
	var restart: Button = game.get_node("%Restart")
	var difficulty: OptionButton = game.get_node("ConfirmNewMatch/Content/Settings/MapSelection/Preview/DifficultyChoice")
	var new_match: Button = game.get_node("%NewMatch")
	var confirm: ConfirmationDialog = game.get_node("ConfirmNewMatch")
	expect(one.disabled and two.disabled and "Thinking" in two.text, "CPU mode exposed player switching or omitted thinking status.")
	expect(difficulty.item_count == 5 and "Normal" in two.text, "Missing current Normal difficulty.")
	check_controls_fit([submit, new_match], [phase])
	choose(board, Vector2.ZERO, MEADOW_BOARD, true)
	expect(not submit.disabled, "Human input was disabled while the CPU was thinking.")
	var board_rect := board.get_global_rect()
	var grid_before: Rect2 = board.call("GetRenderedGridBounds")
	game.set_process(true)
	if not await wait_until(func(): return "Submitted" in two.text):
		return
	expect(not submit.disabled and phase.text == "Draft · Round 1", "CPU submission cleared the human choice or advanced the round.")
	expect(board.get_global_rect() == board_rect and board.call("GetRenderedGridBounds") == grid_before,
		"CPU submission moved the battlefield.")
	expect(piece_count(board) == 0, "A pending CPU coordinate appeared on the battlefield.")
	choose(board, Vector2.ZERO, MEADOW_BOARD, true)
	if not await wait_until(func(): return phase.text == "Main · Turn 1"):
		return
	expect(submit.disabled and piece_count(board) == 3 and
		piece_art_at(board, Vector2(2, 2), MEADOW_BOARD) == "grave.png",
		"Opening resolution did not show both knights and the forced collision or clear the selection.")
	check_banner(board, MEADOW_BOARD, ["0", "0"])
	await capture("main-portrait")

	var moves := [Vector2(0, 0), Vector2(1, 0), Vector2(1, 0), Vector2(2, 0), Vector2(2, 0),
		Vector2(3, 0), Vector2(3, 0), Vector2(4, 0), Vector2(4, 0), Vector2(2, 1),
		Vector2(5, 0), Vector2(5, 0), Vector2(0, 1), Vector2(4, 1), Vector2(0, 1),
		Vector2(1, 1), Vector2(1, 1), Vector2(3, 1), Vector2(3, 1), Vector2(3, 2),
		Vector2(5, 1), Vector2(5, 1), Vector2(0, 2), Vector2(0, 2), Vector2(1, 2),
		Vector2(1, 2), Vector2(5, 2), Vector2(5, 2), Vector2(2, 3), Vector2(2, 3)]
	var scores := [["1", "0"], ["1", "1"], ["2", "1"], ["2", "1"], ["3", "1"]]
	root.size = Vector2i(1000, 690)
	await settle()
	for index in moves.size():
		var previous_phase := phase.text
		choose(board, moves[index], MEADOW_BOARD)
		expect(not submit.disabled, "Expected a legal human target in %s." % previous_phase)
		if index % 2 == 0:
			choose(board, moves[index], MEADOW_BOARD)
		else:
			click(submit.get_global_rect().get_center())
		if not await wait_until(func(): return phase.text != previous_phase):
			return
		if index < scores.size():
			check_banner(board, MEADOW_BOARD, scores[index])
	expect(phase.text == "Match complete" and game.get_node("%Status").text == "CPU wins", "Wrong seeded CPU match outcome.")
	check_banner(board, MEADOW_BOARD, ["11", "12"])
	await capture("finished-landscape")

	click(restart.get_global_rect().get_center())
	await settle()
	expect(phase.text == "Draft · Round 1" and "Normal" in two.text, "Restart changed the difficulty or phase.")
	check_banner(board, MEADOW_BOARD, ["0", "0"])
	game.set_process(false)
	click(new_match.get_global_rect().get_center())
	expect(confirm.visible and difficulty.is_visible_in_tree() and "Normal" in two.text, "New Match bypassed confirmation.")
	await settle()
	var map_list: ScrollContainer = confirm.get_node("Content/Settings/MapSelection/Picker/Scroll")
	var difficulty_rect := difficulty.get_global_rect()
	var list_rect := map_list.get_global_rect()
	expect(difficulty_rect.position.x >= list_rect.end.x and difficulty_rect.end.y <= list_rect.end.y,
		"Landscape difficulty must fit beside the list without taking away list height.")
	expect(map_list.size.y >= 5 * map_list.get_node("Entries/random").size.y,
		"The landscape list has less than five desktop map rows of space.")
	await capture("new-match")
	difficulty.select(4)
	expect("Normal" in two.text, "Changing the selector changed the active profile.")
	confirm.get_cancel_button().pressed.emit()
	await settle()
	expect(not confirm.visible and "Normal" in two.text, "Cancel replaced the active match.")
	var entries := confirm.get_node("Content/Settings/MapSelection/Picker/Scroll/Entries")
	click(new_match.get_global_rect().get_center())
	entries.get_node("broadshield-field").pressed.emit()
	difficulty.select(4)
	confirm.get_ok_button().pressed.emit()
	game.set_process(false)
	await settle()
	expect("Broadshield Field" in game.get_node("%ActiveMap").text and "Monster" in two.text and
		"Thinking" in two.text and phase.text == "Draft · Round 1" and piece_count(board) == 0,
		"CPU map replacement carried over input or work from the old board.")
	check_banner(board, Vector2(12, 10), ["0", "0"])
	click(new_match.get_global_rect().get_center())
	expect(entries.get_node("broadshield-field").button_pressed, "CPU mode forgot the selected map.")
	entries.get_node("open-meadow").pressed.emit()
	confirm.get_ok_button().pressed.emit()
	# StartMatch restores the CPU pump. Pause again before the next frame to inspect the replacement.
	game.set_process(false)
	await settle()
	expect("Monster" in two.text and phase.tooltip_text.contains("Open Meadow"),
		"Confirmed replacement ignored the selected difficulty or fixed map.")
	expect(phase.text == "Draft · Round 1" and "Thinking" in two.text and submit.disabled and piece_count(board) == 0,
		"Work or input from the replaced match reached the new match.")
	check_banner(board, MEADOW_BOARD, ["0", "0"])
	game.set_process(true)
	for collision: Vector2 in [Vector2(5, 4), Vector2(2, 2)]:
		if not await wait_until(func(): return "Submitted" in two.text):
			return
		choose(board, collision, MEADOW_BOARD)
		click(submit.get_global_rect().get_center())
		await settle()
	if not await wait_until(func(): return phase.text == "Draft ended"):
		return
	expect(restart.text == "Restart Draft" and not submit.visible, "A failed draft did not require explicit restart.")
	check_banner(board, MEADOW_BOARD, ["0", "0"])
	click(restart.get_global_rect().get_center())
	await settle()
	expect(phase.text == "Draft · Round 1" and "Monster" in two.text, "Draft restart lost the selected profile.")
	check_banner(board, MEADOW_BOARD, ["0", "0"])
	game.queue_free()
	await process_frame
	await check_knight_support_cpu()
	if not failed:
		print("Verified the seeded Classic CPU match (30 main turns, 11–12 CPU win), Knight Support play, map replacement, restarts, and banner alignment.")
		quit()

func check_knight_support_cpu() -> void:
	var game := spawn("open-meadow", 1, 3)
	game.set_process(false)
	await settle()
	var two: Button = game.get_node("%PlayerTwo")
	var phase: Label = game.get_node("%Phase")
	var badge: Label = game.get_node("%RuleSetBadge")
	var board: Control = game.get_node("%BoardRenderer")
	var difficulty: OptionButton = game.get_node("ConfirmNewMatch/Content/Settings/MapSelection/Preview/DifficultyChoice")
	var new_match: Button = game.get_node("%NewMatch")
	var dialog: ConfirmationDialog = game.get_node("ConfirmNewMatch")
	expect(badge.text == "Knight Support" and "Very Hard" in two.text and "Thinking" in two.text,
		"Knight Support CPU fixture initialized incorrectly.")
	game.set_process(true)
	# The seed gives these CPU choices. The final remaining start then collides automatically.
	for collision: Vector2 in [Vector2(5, 4), Vector2(2, 2)]:
		if not await wait_until(func(): return "Submitted" in two.text):
			return
		choose(board, collision, MEADOW_BOARD)
		choose(board, collision, MEADOW_BOARD)
		await settle()
	expect(phase.text == "Draft ended", "Seeded Knight Support collision did not fail the draft.")
	expect(game.get_node("%Detail").text == "No starting castles were placed.", "Failed draft named the wrong starting piece.")
	click(game.get_node("%Restart").get_global_rect().get_center())
	if not await wait_until(func(): return "Submitted" in two.text):
		return
	expect(badge.text == "Knight Support" and "Very Hard" in two.text, "Restart lost the ruleset or difficulty.")
	# Restart advances the seed stream. This time the CPU selects the opposite corner.
	choose(board, Vector2(2, 2), MEADOW_BOARD)
	choose(board, Vector2(2, 2), MEADOW_BOARD)
	expect(phase.text == "Main · Turn 1", "Seeded Knight Support restart did not reach Main.")
	expect(piece_art_at(board, Vector2.ZERO, MEADOW_BOARD) == "castle_base.png" and
		piece_art_at(board, Vector2(2, 2), MEADOW_BOARD) == "castle_base.png", "CPU match did not render starting castles.")
	check_banner(board, MEADOW_BOARD, ["1", "1"])
	var first_placement := Vector2(1, 2)
	choose(board, first_placement, MEADOW_BOARD)
	choose(board, first_placement, MEADOW_BOARD)
	if not await wait_until(func(): return phase.text == "Main · Turn 2"):
		return
	expect(piece_art_at(board, first_placement, MEADOW_BOARD) == "knight_base.png",
		"A castle-supported placement did not survive the first CPU turn.")
	click(new_match.get_global_rect().get_center())
	expect(difficulty.selected == 3, "Dialog did not remember the live Knight Support difficulty.")
	dialog.get_ok_button().pressed.emit()
	await settle()
	expect(badge.text == "Knight Support" and "Very Hard" in two.text and phase.text == "Draft · Round 1" and piece_count(board) == 0,
		"New Match changed the Knight Support configuration.")
	game.queue_free()
	await process_frame

func spawn(map_id: String, rules: int = -1, difficulty: int = 1) -> Control:
	var game: Control = MATCH_SCENE.instantiate()
	game.set("MapIdOverride", map_id)
	if rules >= 0:
		game.set("Rules", rules)
	game.set("Difficulty", difficulty)
	root.add_child(game)
	return game

func wait_until(condition: Callable) -> bool:
	for frame in TURN_LIMIT:
		if condition.call():
			return true
		await process_frame
	expect(false, "Timed out waiting for a CPU or session state transition.")
	return false

func piece_count(board: Control) -> int:
	return board.get_children().filter(func(child): return child is Sprite2D).size()
