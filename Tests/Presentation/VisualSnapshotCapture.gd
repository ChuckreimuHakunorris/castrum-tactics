extends SceneTree

# Run without --headless. A rendered viewport is needed for image readback.
const SCENE = preload("res://Scenes/main.tscn")
var output: String

func _initialize():
	output = OS.get_environment("CASTRUM_CAPTURE_DIR")
	call_deferred("capture")

func press(game, player, point, size):
	game.get_node("%Player" + player).pressed.emit()
	var board = game.get_node("%BoardRenderer")
	var bounds: Rect2 = board.call("GetRenderedGridBounds")
	var tap := InputEventMouseButton.new()
	tap.button_index = MOUSE_BUTTON_LEFT
	tap.pressed = true
	tap.position = board.get_global_transform_with_canvas() * (bounds.position + (Vector2(point) + Vector2(0.5, 0.5)) * bounds.size / size)
	board.call("_UnhandledInput", tap)
	game.get_node("%Submit").pressed.emit()

func settle():
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw

func capture():
	if output.is_empty() or DirAccess.make_dir_recursive_absolute(output) != OK:
		push_error("Set CASTRUM_CAPTURE_DIR to a writable output directory.")
		quit(1)
		return
	for map_id in ["demonstration", "banner-plain"]:
		for portrait in [false, true]:
			root.size = Vector2i(720, 1000) if portrait else Vector2i(1080, 720)
			var game = SCENE.instantiate()
			game.set("Mode", 1)
			game.get_node("AnimatedBackground").set("MotionEnabled", false)
			game.set("Rules", 0)
			game.set("MapIdOverride", map_id)
			root.add_child(game)
			await settle()
			var prefix: String = output + "/" + map_id + ("-portrait" if portrait else "-landscape")
			var image := root.get_texture().get_image()
			if image.save_png(prefix + "-draft.png") != OK:
				push_error("Could not save draft image for " + map_id)
				quit(1)
				return
			var map: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Maps/" + map_id + ".json"))
			var starts: Array[Vector2i] = []
			for y: int in range(map.height):
				for x: int in range(map.width):
					if map.layout[y][x] == "S":
						starts.append(Vector2i(x,y))
			while starts.size() >= 2:
				press(game, "One", starts.pop_front(), Vector2(map.width, map.height))
				press(game, "Two", starts.pop_front(), Vector2(map.width, map.height))
			await settle()
			if game.get_node("%Phase").text.begins_with("Draft") or root.get_texture().get_image().save_png(prefix + "-resolved.png") != OK:
				push_error("Could not capture resolved draft for " + map_id)
				quit(1)
				return
			var board = game.get_node("%BoardRenderer")
			var sprite_count = 0
			for child in board.get_children():
				if child is Sprite2D:
					sprite_count += 1
			print(map_id, " portrait=",portrait, " bounds=",board.call("GetRenderedGridBounds"), " sprites=",sprite_count,
				" resources=",Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT),
				" nodes=",Performance.get_monitor(Performance.OBJECT_NODE_COUNT),
				" static_memory=",Performance.get_monitor(Performance.MEMORY_STATIC))
			game.queue_free()
			await settle()
	quit()
