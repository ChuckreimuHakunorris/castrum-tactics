extends SceneTree

const MATCH_SCENE := preload("res://Scenes/main.tscn")
const WINDOW_SIZES := [Vector2i(900, 650), Vector2i(720, 1080), Vector2i(320, 240)]

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	for window_size in WINDOW_SIZES:
		root.size = window_size
		for player in ["One", "Two"]:
			var game := MATCH_SCENE.instantiate()
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
	quit()

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

	var original: Array[Rect2] = []
	for control in controls:
		original.append(control.get_global_rect())

	# Select B2 through the live viewport, then submit through the actual button.
	var pitch := 0.96 * minf(board.size.x / 7.0, board.size.y / 5.0)
	var origin := (board.size - Vector2(7, 5) * pitch) / 2.0
	click(board.get_global_transform_with_canvas() * (origin + Vector2(1.5, 1.5) * pitch))
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
	return true

func click(point: Vector2) -> void:
	var event := InputEventMouseButton.new()
	event.position = point
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = true
	root.push_input(event, true)
	event = event.duplicate() as InputEventMouseButton
	event.pressed = false
	root.push_input(event, true)

func settle() -> void:
	for frame in 6:
		await process_frame
