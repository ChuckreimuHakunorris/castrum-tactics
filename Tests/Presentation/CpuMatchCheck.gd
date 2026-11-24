extends "res://Tests/Presentation/DraftLayoutCheck.gd"

const SMALL_BOARD := Vector2(3, 3)
const RECTANGULAR_BOARD := Vector2(7, 5)
const TURN_LIMIT := 2400

func run() -> void:
	root.size = Vector2i(1000, 690)
	var rectangular := spawn("demonstration")
	await settle()
	expect(rectangular.get_node("%PlayerOne").text.begins_with("You") and rectangular.get_node("%PlayerTwo").text.begins_with("CPU"),
		"Normal startup did not default to Versus CPU.")
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

	var game := spawn("pocket-meadow")
	# Hold the main-thread pump so the human can highlight a tile before CPU submission.
	game.set_process(false)
	await settle()
	var one: Button = game.get_node("%PlayerOne")
	var two: Button = game.get_node("%PlayerTwo")
	var phase: Label = game.get_node("%Phase")
	var board: Control = game.get_node("%BoardRenderer")
	var submit: Button = game.get_node("%Submit")
	var restart: Button = game.get_node("%Restart")
	var difficulty: OptionButton = game.get_node("%DifficultyChoice")
	var new_match: Button = game.get_node("%NewMatch")
	var confirm: ConfirmationDialog = game.get_node("ConfirmNewMatch")
	expect(one.disabled and two.disabled and "Thinking" in two.text, "CPU mode exposed player switching or omitted thinking status.")
	expect(difficulty.item_count == 5 and "Normal" in two.text, "Missing current Normal difficulty.")
	check_controls_fit([submit, new_match], [phase])
	choose(board, Vector2.ZERO, SMALL_BOARD, true)
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
	choose(board, Vector2.ZERO, SMALL_BOARD, true)
	if not await wait_until(func(): return phase.text == "Main · Turn 1"):
		return
	expect(submit.disabled and piece_count(board) == 2, "Opening resolution did not show both knights and clear the selection.")
	check_banner(board, SMALL_BOARD, ["0", "0"])
	await capture("main-portrait")

	var moves := [Vector2(0, 0), Vector2(1, 0), Vector2(1, 0), Vector2(0, 1), Vector2(0, 1), Vector2(0, 2),
		Vector2(1, 2), Vector2(2, 1), Vector2(2, 0), Vector2(2, 1), Vector2(1, 2)]
	var scores := [["1", "0"], ["1", "0"], ["1", "0"], ["1", "1"], ["2", "2"]]
	root.size = Vector2i(1000, 690)
	await settle()
	for index in moves.size():
		var previous_phase := phase.text
		choose(board, moves[index], SMALL_BOARD)
		expect(not submit.disabled, "Expected a legal human target in %s." % previous_phase)
		if index % 2 == 0:
			choose(board, moves[index], SMALL_BOARD)
		else:
			click(submit.get_global_rect().get_center())
		if not await wait_until(func(): return phase.text != previous_phase):
			return
		if index < scores.size():
			check_banner(board, SMALL_BOARD, scores[index])
	expect(phase.text == "Match complete" and game.get_node("%Status").text == "Draw", "Wrong seeded CPU match outcome.")
	check_banner(board, SMALL_BOARD, ["2", "2"])
	await capture("finished-landscape")

	click(restart.get_global_rect().get_center())
	await settle()
	expect(phase.text == "Draft · Round 1" and "Normal" in two.text, "Restart changed the difficulty or phase.")
	check_banner(board, SMALL_BOARD, ["0", "0"])
	game.set_process(false)
	click(new_match.get_global_rect().get_center())
	expect(confirm.visible and difficulty.is_visible_in_tree() and "Normal" in two.text, "New Match bypassed confirmation.")
	await capture("new-match")
	difficulty.select(4)
	expect("Normal" in two.text, "Changing the selector changed the active profile.")
	confirm.get_cancel_button().pressed.emit()
	await settle()
	expect(not confirm.visible and "Normal" in two.text, "Cancel replaced the active match.")
	click(new_match.get_global_rect().get_center())
	difficulty.select(4)
	confirm.get_ok_button().pressed.emit()
	# StartMatch restores the CPU pump. Pause again before the next frame to inspect the replacement.
	game.set_process(false)
	await settle()
	expect("Monster" in two.text and phase.tooltip_text.contains("Pocket Meadow"),
		"Confirmed replacement ignored the selected difficulty or fixed map.")
	expect(phase.text == "Draft · Round 1" and "Thinking" in two.text and submit.disabled and piece_count(board) == 0,
		"Work or input from the replaced match reached the new match.")
	check_banner(board, SMALL_BOARD, ["0", "0"])
	game.set_process(true)
	if not await wait_until(func(): return "Submitted" in two.text):
		return
	choose(board, Vector2(2, 2), SMALL_BOARD)
	click(submit.get_global_rect().get_center())
	if not await wait_until(func(): return phase.text == "Draft ended"):
		return
	expect(restart.text == "Restart Draft" and not submit.visible, "A failed draft did not require explicit restart.")
	check_banner(board, SMALL_BOARD, ["0", "0"])
	click(restart.get_global_rect().get_center())
	await settle()
	expect(phase.text == "Draft · Round 1" and "Monster" in two.text, "Draft restart lost the selected profile.")
	check_banner(board, SMALL_BOARD, ["0", "0"])
	game.queue_free()
	await process_frame
	if not failed:
		print("Verified seeded Normal CPU play on Pocket Meadow (11 main turns, 2–2 draw), restart, confirmed replacement, failed draft, and banner alignment.")
		quit()

func spawn(map_id: String) -> Control:
	var game: Control = MATCH_SCENE.instantiate()
	game.set("MapIdOverride", map_id)
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

func capture(file_name: String) -> void:
	var directory := OS.get_environment("CASTRUM_M8B_SCREENSHOTS")
	if directory.is_empty():
		return
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	expect(image != null and image.save_png(directory.path_join(file_name + ".png")) == OK,
		"Could not save the %s screenshot." % file_name)
