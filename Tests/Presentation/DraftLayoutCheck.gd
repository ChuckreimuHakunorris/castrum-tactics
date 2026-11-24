extends SceneTree

const MATCH_SCENE := preload("res://Scenes/main.tscn")
const WINDOW_SIZES := [Vector2i(900, 650), Vector2i(720, 1080), Vector2i(320, 240)]
var failed := false
var mouse_event := InputEventMouseButton.new()
var touch_event := InputEventScreenTouch.new()

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	for window_size in WINDOW_SIZES:
		root.size = window_size
		for player in ["One", "Two"]:
			var game := MATCH_SCENE.instantiate()
			game.set("MapIdOverride", "demonstration")
			for label_name in ["%Status", "%Detail"]:
				var label: Label = game.get_node(label_name)
				if label.custom_maximum_size.x <= 0:
					push_error("%s is missing a maximum width in the editor scene." % label_name)
					game.free()
					quit(1)
					return
			root.add_child(game)
			var passed := await check_draft_submission(game, player)
			game.queue_free()
			await process_frame
			if not passed:
				quit(1)
				return
	print("Draft layout stayed fixed for both players at all three window sizes.")
	var game := MATCH_SCENE.instantiate()
	game.set("MapIdOverride", "demonstration")
	root.size = Vector2i(720, 1080)
	root.add_child(game)
	await check_complete_match(game)
	game.queue_free()
	await process_frame
	if not failed:
		await check_board_extremes()
	if not failed:
		print("Verified draft revision, resize, main turns, final scores, restart, and both board size extremes.")
		quit()

func check_board_extremes() -> void:
	for sample in [["pocket-meadow", 3, Vector2(2, 2)], ["twelve-towers", 12, Vector2(11, 11)]]:
		var edge: int = sample[1]
		var dimensions := Vector2(edge, edge)
		root.size = Vector2i(360, 640)
		var game := MATCH_SCENE.instantiate()
		game.set("MapIdOverride", sample[0])
		root.add_child(game)
		await settle()
		var board: Control = game.get_node("%BoardRenderer")
		var submit: Button = game.get_node("%Submit")
		var phase: Label = game.get_node("%Phase")
		var one: Button = game.get_node("%PlayerOne")
		var two: Button = game.get_node("%PlayerTwo")
		expect(phase.tooltip_text.ends_with("(%d × %d)" % [edge, edge]), "Wrong map size loaded.")
		if edge == 12:
			choose(board, Vector2(2, 2), dimensions)
			await settle()
			expect(submit.disabled, "An inaccessible tile was selectable.")
		choose(board, Vector2.ZERO, dimensions, true)
		await settle()
		expect(not submit.disabled, "Top-left start did not register on %s." % sample[0])
		root.size = Vector2i(1280, 720)
		await settle()
		expect(not submit.disabled, "Resize cleared the choice on %s." % sample[0])
		click(submit.get_global_rect().get_center())
		await settle()
		click(two.get_global_rect().get_center())
		await settle()
		choose(board, sample[2], dimensions)
		await settle()
		expect(not submit.disabled, "Bottom-right start did not register on %s." % sample[0])
		click(submit.get_global_rect().get_center())
		await settle()
		var expected_phase := "Main · Turn 1" if edge == 3 else "Draft · Round 2"
		expect(phase.text == expected_phase, "Opening round did not resolve on %s." % sample[0])
		if edge == 12:
			for y in [0, 3, 8, 11]:
				for x in [0, 5, 11]:
					var position := Vector2(x, y)
					if phase.text.begins_with("Draft") and position != Vector2.ZERO and position != sample[2]:
						await play(board, one, submit, position, dimensions)
						await play(board, two, submit, position, dimensions)
			expect(phase.text == "Main · Turn 1", "Large-board draft did not advance to Main.")
			await play(board, one, submit, Vector2.ZERO, dimensions)
			expect(submit.text == "Revise selection", "Large-board main submission did not enter the waiting state.")
			await play(board, two, submit, sample[2], dimensions)
			expect(phase.text in ["Main · Turn 2", "Match complete"], "Large-board main turn did not resolve.")
		game.queue_free()
		await process_frame

func expect(condition: bool, message: String) -> void:
	if not condition:
		failed = true
		push_error(message)
		quit(1)

func check_complete_match(game: Control) -> void:
	var one: Button = game.get_node("%PlayerOne")
	var two: Button = game.get_node("%PlayerTwo")
	var submit: Button = game.get_node("%Submit")
	var restart: Button = game.get_node("%Restart")
	var detail: Label = game.get_node("%Detail")
	var phase: Label = game.get_node("%Phase")
	var board: Control = game.get_node("%BoardRenderer")
	await settle()
	choose(board, Vector2(4, 0))
	expect(not submit.disabled, "Draft selection did not enable submit.")
	click(submit.get_global_rect().get_center())
	await settle()
	choose(board, Vector2(1, 1))
	expect(not submit.disabled and submit.text == "Revise selection", "Pending draft selection could not be revised.")
	expect(board.get_parent().vertical, "Expected the initial stacked layout.")
	root.size = Vector2i(1280, 720)
	await settle()
	expect(not board.get_parent().vertical, "Expected the sidebar after resizing.")
	expect(not submit.disabled, "Resizing cleared the provisional revision.")
	var sidebar_rect := board.get_global_rect()
	click(submit.get_global_rect().get_center())
	await settle()
	expect(detail.text == "Waiting for Player Two." and "Submitted" in one.text, "Revision lost the waiting state.")
	expect(board.get_global_rect() == sidebar_rect, "Revision moved the board.")
	await play(board, two, submit, Vector2(3, 2))
	expect(two.button_pressed and phase.text == "Draft · Round 2", "Switching players did not complete the draft round.")

	# Reusing the original E1 target proves the first player submitted the revision to B2.
	await play(board, one, submit, Vector2(4, 0))
	await play(board, two, submit, Vector2(5, 3))
	expect(phase.text == "Main · Turn 1", "The opening did not advance to the main phase.")
	var main_rect := board.get_global_rect()
	await play(board, one, submit, Vector2(2, 2))
	expect(detail.text == "Waiting for Player Two." and submit.text == "Revise selection", "Missing main-phase waiting state.")
	expect(board.get_global_rect() == main_rect, "Main-phase submission moved the board.")
	check_controls_fit([one, two, submit], [detail, phase])
	await play(board, two, submit, Vector2(4, 1))
	var remaining_turns := [
		[Vector2(1, 2), Vector2(4, 0)],
		[Vector2(1, 0), Vector2(6, 2)],
		[Vector2(0, 0), Vector2(3, 2)],
		[Vector2(2, 2), Vector2(2, 2)],
		[Vector2(0, 1), Vector2(3, 1)],
		[Vector2(1, 2), Vector2(3, 3)],
		[Vector2(2, 3), Vector2(2, 3)],
	]
	for turn in remaining_turns:
		await play(board, one, submit, turn[0])
		await play(board, two, submit, turn[1])
	var heading: Label = game.get_node("%Status")
	var first_score: Label = game.get_node("%PlayerOneScore")
	var second_score: Label = game.get_node("%PlayerTwoScore")
	expect(phase.text == "Match complete" and heading.text == "Player Two wins", "Missing final outcome.")
	expect(first_score.text == "8" and second_score.text == "18", "Missing expected final scores.")
	expect(restart.text == "Restart Match" and not submit.visible and board.visible, "Incorrect finished controls.")
	check_controls_fit([restart], [heading, phase, first_score, second_score])
	click(restart.get_global_rect().get_center())
	await settle()
	expect(one.button_pressed and phase.text == "Draft · Round 1" and submit.disabled, "Restart did not restore the opening.")

func check_controls_fit(buttons: Array[Button], labels: Array[Label]) -> void:
	for button in buttons:
		expect(button.is_visible_in_tree() and root.get_visible_rect().encloses(button.get_global_rect()),
			"Button is outside the viewport: %s" % button.text)
	for label in labels:
		expect(label.is_visible_in_tree() and root.get_visible_rect().encloses(label.get_global_rect()),
			"Label is outside the viewport: %s" % label.text)
		expect(label.get_visible_line_count() == label.get_line_count(), "Label is clipped: %s" % label.text)

func play(board: Control, player: Button, submit: Button, coordinate: Vector2, dimensions := Vector2(7, 5)) -> void:
	click(player.get_global_rect().get_center())
	await settle()
	expect(player.button_pressed, "Player could not be activated.")
	choose(board, coordinate, dimensions)
	expect(not submit.disabled, "Expected a legal choice at %s." % coordinate)
	click(submit.get_global_rect().get_center())
	await settle()

func choose(board: Control, coordinate: Vector2, dimensions := Vector2(7, 5), touch := false) -> void:
	var pitch := 0.96 * minf(board.size.x / dimensions.x, board.size.y / dimensions.y)
	var origin := (board.size - dimensions * pitch) / 2.0
	var point := board.get_global_transform_with_canvas() * (origin + (coordinate + Vector2(0.5, 0.5)) * pitch)
	if touch:
		touch_event.position = point
		touch_event.pressed = true
		root.push_input(touch_event, true)
		touch_event.pressed = false
		root.push_input(touch_event, true)
	else:
		click(point)

func check_draft_submission(game: Node, player: String) -> bool:
	var board: Control = game.get_node("%BoardRenderer")
	var detail: Label = game.get_node("%Detail")
	var status: Label = game.get_node("%Status")
	var submit: Button = game.get_node("%Submit")
	var panel: Control = game.get_node("Frame/Layout/Controls")
	var controls: Array[Control] = [
		board, panel, detail.get_parent(), game.get_node("%PlayerOne"), game.get_node("%PlayerTwo"), submit
	]
	await settle()
	var text_width := floorf(detail.get_parent().size.x)
	if detail.get_combined_maximum_size().x != text_width or status.get_combined_maximum_size().x != text_width:
		push_error("The wrapped labels did not use the available panel width.")
		return false
	if player == "Two":
		click(game.get_node("%PlayerTwo").get_global_rect().get_center())
		await settle()
	if detail.text != "Select a gold dot, then submit.":
		push_error("The opening instruction did not appear.")
		return false

	check_controls_fit([game.get_node("%PlayerOne"), game.get_node("%PlayerTwo"), submit], [detail])
	var original: Array[Rect2] = []
	for control in controls:
		original.append(control.get_global_rect())

	choose(board, Vector2(1, 1))
	await settle()
	if submit.disabled:
		push_error("The draft target did not enable the Submit button.")
		return false

	click(submit.get_global_rect().get_center())
	for frame in 6:
		await process_frame
		for index in controls.size():
			var current := controls[index].get_global_rect()
			if current != original[index]:
				push_error("%s: Player %s submission moved %s from %s to %s." % [
					root.size, player, controls[index].name, original[index], current
				])
				return false

	var opponent := "Two" if player == "One" else "One"
	if detail.text != "Waiting for Player %s." % opponent or submit.text != "Revise selection":
		push_error("The waiting instruction and revise action did not appear.")
		return false
	check_controls_fit([game.get_node("%PlayerOne"), game.get_node("%PlayerTwo"), submit], [detail])
	return not failed

func click(point: Vector2) -> void:
	mouse_event.position = point
	mouse_event.button_index = MOUSE_BUTTON_LEFT
	mouse_event.pressed = true
	root.push_input(mouse_event, true)
	mouse_event.pressed = false
	root.push_input(mouse_event, true)

func settle() -> void:
	for frame in 6:
		await process_frame
