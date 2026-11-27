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
			game.set("Mode", 1) # LocalMultiplayer
			game.set("Rules", 0) # Classic
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
	game.set("Mode", 1)
	game.set("Rules", 0)
	game.set("MapIdOverride", "demonstration")
	game.call("SetTemporaryPresentation", true, true, true)
	root.size = Vector2i(720, 1080)
	root.add_child(game)
	await check_complete_match(game)
	game.queue_free()
	await process_frame
	if not failed:
		await check_board_extremes()
	if not failed:
		await check_knight_support_local()
	if not failed:
		await check_map_selection()
	if not failed:
		await check_visual_sidecar()
	if not failed:
		await check_connected_castles()
	if not failed:
		await check_interaction_indicators()
	if not failed:
		print("Verified the Classic walkthrough, Knight Support eliminations and scores, draft revision, restarts, map selection and previews.")
		quit()

func check_interaction_indicators() -> void:
	var game := MATCH_SCENE.instantiate()
	game.set("Mode", 1)
	game.set("Rules", 0)
	game.set("MapIdOverride", "demonstration")
	game.call("SetTemporaryPresentation", true, false, true)
	root.size = Vector2i(1080, 720)
	root.add_child(game)
	await settle()
	var board: Control = game.get_node("%BoardRenderer")
	var banner: Control = board.get_node("TerritoryScoreBanner")
	var targets: Node2D = board.get_node("LegalTargets")
	var one: Button = game.get_node("%PlayerOne")
	var two: Button = game.get_node("%PlayerTwo")
	var submit: Button = game.get_node("%Submit")
	var background: Control = game.get_node("AnimatedBackground")
	var liquid: ColorRect = banner.get_node("Liquid")
	expect(targets.material == null and not liquid.visible and banner.call("GetClaimableArea") == 0,
		"Draft should show neutral shields and an empty banner.")
	await capture("10c-draft")
	await play(board, one, submit, Vector2(1, 1))
	await play(board, two, submit, Vector2(3, 2))
	await play(board, one, submit, Vector2(4, 0))
	await play(board, two, submit, Vector2(5, 3))
	click(one.get_global_rect().get_center())
	await settle()
	var examples := {}
	for y in 5:
		for x in 7:
			examples[board.call("ReadLegalTargetKind", x, y)] = Vector2i(x, y)
	expect(examples.size() == 4 and banner.call("GetClaimableArea") > 0,
		"All four target classifications and the Main denominator should be available.")
	if examples.size() != 4:
		game.queue_free()
		return
	var active_only: Vector2i = examples[1]
	var opponent_only: Vector2i = examples[2]
	var shared: Vector2i = examples[3]
	expect(targets.material.get_shader_parameter("active_color").is_equal_approx(Color("#f06c68")) and
		targets.material.get_shader_parameter("opposing_color").is_equal_approx(Color("#69a4ff")),
		"Player One did not set the shared shader palette.")
	choose(board, opponent_only, Vector2(7, 5), true)
	expect(submit.disabled, "An opponent-only frame accepted a touch.")
	choose(board, shared)
	expect(not submit.disabled, "A shared frame did not accept an active player's choice.")
	await capture("10c-red-shared")
	click(two.get_global_rect().get_center())
	expect(submit.disabled and board.call("ReadLegalTargetKind", active_only.x, active_only.y) == 2 and
		board.call("ReadLegalTargetKind", shared.x, shared.y) == 3 and
		targets.material.get_shader_parameter("active_color").is_equal_approx(Color("#69a4ff")),
		"Changing the active player did not reverse the territory targets and palette.")
	choose(board, shared, Vector2(7, 5), true)
	await capture("10c-blue-shared")
	game.call("SetTemporaryPresentation", false, true, true)
	expect(board.call("ReadLegalTargetKind", active_only.x, active_only.y) == 0 and
		board.call("ReadLegalTargetKind", shared.x, shared.y) == 1 and not submit.disabled,
		"Changing opponent visibility altered selection or left opposing colors visible.")
	expect(targets.material.shader.resource_path.ends_with("legal_frame_static.gdshader") and
		liquid.material.shader.resource_path.ends_with("territory_tube_static.gdshader") and
		not background.get("MotionEnabled"), "Reduce motion did not switch all three effects immediately.")
	await capture("10c-static-no-opponent")
	game.call("SetTemporaryPresentation", true, false, false)
	expect(targets.material.shader.resource_path.ends_with("legal_frame.gdshader") and background.get("MotionEnabled"),
		"Disabling future resolution effects also disabled ambient motion.")
	expect(board.call("ReadLegalTargetKind", active_only.x, active_only.y) == 2 and not submit.disabled,
		"Restoring display preferences changed the active input state.")
	game.queue_free()
	await process_frame

func check_visual_sidecar() -> void:
	root.size = Vector2i(900, 650)
	var fixture := MATCH_SCENE.instantiate()
	fixture.set("Mode", 1)
	fixture.set("MapIdOverride", "open-meadow")
	fixture.call("SetVisualDescriptor", "open-meadow",
		'{"version":1,"width":6,"height":5,"tiles":[{"x":0,"y":0,"surface":"grass","variant":"weathered","prop":"stone-accent"}]}')
	root.add_child(fixture)
	await settle()
	var board: Control = fixture.get_node("%BoardRenderer")
	expect(board.call("DescribeVisualTile", 0, 0) == "Weathered/False/True", "Sidecar did not reach the renderer.")
	expect(board.call("DescribeVisualTile", 1, 0) == "Original/False/False", "Unspecified tile lost legacy styling.")
	for path: String in board.call("ReadTerrainCatalogPaths"):
		var texture = load(path)
		expect(texture != null and texture.get_size() == Vector2(128, 128), "Missing terrain texture " + path)
	for mask in [1, 4, 5, 6, 7, 10, 12, 13, 14, 15]:
		var base = load("res://Assets/Sprites/Board/wall-%d_base.png" % mask)
		var tint = load("res://Assets/Sprites/Board/wall-%d_mask.png" % (15 if mask & 4 else 10))
		expect(base != null and tint != null and base.get_size() == tint.get_size(), "Misaligned castle mask %d" % mask)
	var one: Button = fixture.get_node("%PlayerOne")
	var two: Button = fixture.get_node("%PlayerTwo")
	var submit: Button = fixture.get_node("%Submit")
	await play(board, one, submit, Vector2.ZERO, Vector2(6, 5))
	await play(board, two, submit, Vector2.ZERO, Vector2(6, 5))
	click(fixture.get_node("%Restart").get_global_rect().get_center())
	root.size = Vector2i(660, 1000)
	await settle()
	expect(board.call("DescribeVisualTile", 0, 0) == "Weathered/False/True", "Visuals changed after draft restart or resize.")
	fixture.queue_free()
	await process_frame

func check_connected_castles() -> void:
	root.size = Vector2i(900, 650)
	var game := MATCH_SCENE.instantiate()
	game.set("Mode", 1)
	game.set("MapIdOverride", "open-meadow")
	root.add_child(game)
	await settle()
	var board: Control = game.get_node("%BoardRenderer")
	var one: Button = game.get_node("%PlayerOne")
	var two: Button = game.get_node("%PlayerTwo")
	var submit: Button = game.get_node("%Submit")
	var dimensions := Vector2(6, 5)
	await play(board, one, submit, Vector2.ZERO, dimensions)
	await play(board, two, submit, Vector2(2, 2), dimensions)
	await play(board, one, submit, Vector2(1, 0), dimensions)
	await play(board, two, submit, Vector2(2, 1), dimensions)
	await play(board, one, submit, Vector2(1, 0), dimensions)
	await play(board, two, submit, Vector2(3, 2), dimensions)
	expect(piece_art_at(board, Vector2.ZERO, dimensions) == "wall-1_base.png" and
		piece_art_at(board, Vector2(1, 0), dimensions) == "wall-1_base.png",
		"Resolved friendly castles did not join along their shared edge.")
	expect(piece_art_at(board, Vector2(2, 2), dimensions) == "castle_base.png", "An isolated castle acquired a wall.")
	await capture("connected-castles")
	await play(board, one, submit, Vector2(0, 1), dimensions)
	await play(board, two, submit, Vector2(2, 1), dimensions)
	await play(board, one, submit, Vector2(0, 1), dimensions)
	await play(board, two, submit, Vector2(3, 2), dimensions)
	expect(piece_art_at(board, Vector2.ZERO, dimensions) == "wall-4_base.png" and
		piece_art_at(board, Vector2(1, 0), dimensions) == "wall-1_base.png",
		"The roof did not add a rampart beside the eastern facade.")
	await play(board, one, submit, Vector2(1, 1), dimensions)
	await play(board, two, submit, Vector2(2, 3), dimensions)
	await play(board, one, submit, Vector2(1, 1), dimensions)
	await play(board, two, submit, Vector2(2, 3), dimensions)
	expect(piece_art_at(board, Vector2.ZERO, dimensions) == "wall-6_base.png" and
		piece_art_at(board, Vector2(1, 0), dimensions) == "wall-12_base.png",
		"The roof retained its eastern rampart after the neighboring facade became a roof.")
	game.queue_free()
	await process_frame

func check_knight_support_local() -> void:
	var dimensions := Vector2(6, 5)
	root.size = Vector2i(410, 720)
	var game := MATCH_SCENE.instantiate()
	game.set("Mode", 1)
	game.set("MapIdOverride", "open-meadow")
	root.add_child(game)
	await settle()
	var board: Control = game.get_node("%BoardRenderer")
	var phase: Label = game.get_node("%Phase")
	var badge: Label = game.get_node("%RuleSetBadge")
	var one: Button = game.get_node("%PlayerOne")
	var two: Button = game.get_node("%PlayerTwo")
	var submit: Button = game.get_node("%Submit")
	expect(badge.text == "Knight Support", "Local startup did not default to Knight Support.")
	await play(board, one, submit, Vector2.ZERO, dimensions)
	await play(board, two, submit, Vector2(2, 2), dimensions)
	expect(phase.text == "Main · Turn 1", "Knight Support did not enter Main after the draft.")
	expect(piece_art_at(board, Vector2.ZERO, dimensions) == "castle_base.png" and
		piece_art_at(board, Vector2(2, 2), dimensions) == "castle_base.png", "Starting castles were not rendered.")
	check_banner(board, dimensions, ["1", "1"])
	await play(board, one, submit, Vector2(1, 0), dimensions)
	await play(board, two, submit, Vector2(2, 1), dimensions)
	expect(piece_art_at(board, Vector2(1, 0), dimensions) == "knight_base.png", "First-turn castle support failed.")
	await play(board, one, submit, Vector2(2, 0), dimensions)
	expect(piece_art_at(board, Vector2(2, 0), dimensions).is_empty(), "Pending placement was rendered.")
	await play(board, two, submit, Vector2(1, 0), dimensions)
	expect(piece_art_at(board, Vector2(2, 0), dimensions) == "grave.png", "Unsupported placement did not render a grave.")
	expect(board.call("ReadGroundDamage", 2, 0) == 1 and board.call("ReadGroundDamage", 1, 2) == 0,
		"Ground damage was not independent of piece status.")
	expect(board.call("ReadDamageMarkerCount", 2, 0) == 0, "A Knight Support grave drew damage markers.")
	expect(piece_art_at(board, Vector2(1, 0), dimensions) == "knight_base.png", "Supported capture did not survive.")
	check_banner(board, dimensions, ["1", "1"])
	await capture("knight-support-mixed-portrait")

	root.size = Vector2i(1000, 690)
	await settle()
	for collision: Vector2 in [Vector2(1, 1), Vector2(0, 1), Vector2(1, 0)]:
		await play(board, one, submit, collision, dimensions)
		await play(board, two, submit, collision, dimensions)
	expect(phase.text == "Match complete" and game.get_node("%Status").text == "Player Two wins",
		"Knight Support showed the wrong final outcome.")
	check_banner(board, dimensions, ["1", "24"])
	click(game.get_node("%Restart").get_global_rect().get_center())
	await settle()
	expect(phase.text == "Draft · Round 1" and badge.text == "Knight Support", "Restart lost the active ruleset.")
	check_banner(board, dimensions, ["0", "0"])
	game.queue_free()
	await process_frame

func piece_art_at(board: Control, coordinate: Vector2, dimensions: Vector2) -> String:
	var grid: Rect2 = board.call("GetRenderedGridBounds")
	var center := grid.position + (coordinate + Vector2(0.5, 0.5)) * grid.size / dimensions
	for child in board.get_children():
		if child is Sprite2D and child.position.is_equal_approx(center):
			return child.texture.resource_path.get_file()
	return ""

func check_map_selection() -> void:
	var game := MATCH_SCENE.instantiate()
	game.set("Mode", 1)
	game.set("Rules", 0)
	game.set("MapIdOverride", "demonstration")
	game.call("SetTemporaryPresentation", true, true, true)
	root.add_child(game)
	var active_map: Label = game.get_node("%ActiveMap")
	var badge: Label = game.get_node("%RuleSetBadge")
	var board: Control = game.get_node("%BoardRenderer")
	var dialog: ConfirmationDialog = game.get_node("ConfirmNewMatch")
	var picker := dialog.get_node("Content/Settings/MapSelection/Picker")
	var entries := picker.get_node("Scroll/Entries")
	var filter: LineEdit = picker.get_node("Filter")
	var caption: Label = dialog.get_node("Content/Settings/MapSelection/Preview/Caption")
	var preview: Control = dialog.get_node("Content/Settings/MapSelection/Preview/Board")
	var new_match: Button = game.get_node("%NewMatch")
	expect(entries.get_child_count() == 101, "The list must contain Random Map and every predefined battlefield.")
	for retired in ["pocket-meadow", "little-crossing", "serpent-cut"]:
		expect(not entries.has_node(retired), "A retired battlefield is still selectable: %s." % retired)
	expect(entries.has_node("wrenfield"), "The replacement battlefield is missing.")
	var previous := ""
	for entry: Button in entries.get_children().slice(1):
		expect(previous.naturalnocasecmp_to(entry.text) <= 0, "Map entries are out of order.")
		previous = entry.text

	for window_size: Vector2i in [Vector2i(410, 720), Vector2i(410, 530), Vector2i(1000, 500), Vector2i(320, 240)]:
		root.size = window_size
		await settle()
		click(new_match.get_global_rect().get_center())
		await settle()
		expect(dialog.visible and "Leave this match" in dialog.get_node("Content/Settings/Prompt").text and
			not dialog.get_node("Content/Settings/MapSelection/Preview/DifficultyChoice").visible, "The local dialog omitted confirmation or exposed CPU difficulty.")
		expect(entries.get_node("demonstration").button_pressed, "Startup map override was not preselected.")
		expect(preview.call("GetVisualTerrain", 2, 0) == "mountain" and board.call("DescribeVisualTile", 2, 0) == "mountain/plain",
			"The embedded descriptor did not load in both the board and preview.")
		expect(entries.get_node("demonstration").get_global_rect().intersects(picker.get_node("Scroll").get_global_rect()),
			"The startup map was selected but outside the visible list.")
		await check_map_list_scroll(dialog, picker.get_node("Scroll"), entries, caption)
		for sample in [["flint-crescent", Vector2(10, 10), Vector2i(5, 1), Vector2i(3, 1)],
				["granite-orchard", Vector2(12, 10), Vector2i(2, 2), Vector2i(1, 1)]]:
			var entry: Button = entries.get_node(sample[0])
			filter.text = entry.text.split(" — ")[0]
			filter.text_changed.emit(filter.text)
			await settle()
			expect(entries.get_children().filter(func(child): return child.visible).size() == 2,
				"The filter did not narrow the list to one map plus Random Map.")
			entry.pressed.emit()
			await settle()
			var bounds: Rect2 = preview.call("GetGridBounds")
			var dimensions: Vector2 = sample[1]
			expect(bounds.has_area() and is_equal_approx(bounds.size.x / dimensions.x, bounds.size.y / dimensions.y),
				"The selected map preview did not keep square tiles.")
			expect(caption.text == entry.text and preview.call("GetTileKind", 0, 0) == "Ground" and
				preview.call("GetTileKind", sample[2].x, sample[2].y) == "Rock" and
				preview.call("GetTileKind", sample[3].x, sample[3].y) == "Start",
				"The preview did not show the selected map's name, dimensions, ground, rocks, or starts.")
			expect(active_map.text.begins_with("Demonstration") and badge.text == "Classic",
				"Editing the dialog changed the active match.")
			if window_size == Vector2i(410, 720) and sample[0] == "flint-crescent":
				await capture("new-match-portrait")
		var local_window := Rect2(Vector2.ZERO, Vector2(dialog.size))
		var viewport: Rect2 = game.get_viewport_rect()
		var dialog_rect := Rect2(Vector2(dialog.position), Vector2(dialog.size))
		var ok := dialog.get_ok_button().get_global_rect()
		var cancel := dialog.get_cancel_button().get_global_rect()
		expect(viewport.encloses(dialog_rect) and local_window.encloses(ok) and local_window.encloses(cancel)
			and not ok.intersects(cancel), "Dialog or confirmation controls overflowed at %s: %s" % [window_size, dialog_rect])
		if window_size == Vector2i(410, 720):
			expect(dialog.size.y == 660, "The portrait dialog did not use its taller layout.")
		elif window_size == Vector2i(410, 530):
			expect(dialog.size.y < 660, "The portrait dialog must shrink when vertical space is limited.")
		if window_size == Vector2i(1000, 500):
			await capture("new-match-landscape")
		dialog.get_cancel_button().pressed.emit()
		await settle()
		expect(not dialog.visible and active_map.text.begins_with("Demonstration"), "Cancel changed the battlefield.")

	root.size = Vector2i(1000, 690)
	await settle()
	click(new_match.get_global_rect().get_center())
	await settle()
	entries.get_node("granite-orchard").pressed.emit()
	dialog.get_ok_button().pressed.emit()
	await settle()
	expect(active_map.text == "Granite Orchard — 12 × 10" and badge.text == "Classic",
		"The explicit map did not replace the startup override or preserve Classic.")
	check_banner(board, Vector2(12, 10), ["0", "0"])
	var background: Control = game.get_node("AnimatedBackground")
	expect(not background.get("MotionEnabled"), "Replacement lost the presentation preferences.")
	game.call("SetTemporaryPresentation", true, false, true)
	expect(background.get("MotionEnabled"), "The replacement did not receive a live preference change.")
	await capture("battlefield-granite-orchard")
	var one: Button = game.get_node("%PlayerOne")
	var two: Button = game.get_node("%PlayerTwo")
	var submit: Button = game.get_node("%Submit")
	var definition: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Maps/granite-orchard.json"))
	for y in definition.layout.size():
		for x in int(definition.width):
			if definition.layout[y][x] == "S" and submit.visible:
				await play(board, one, submit, Vector2(x, y), Vector2(12, 10))
				await play(board, two, submit, Vector2(x, y), Vector2(12, 10))
	click(game.get_node("%Restart").get_global_rect().get_center())
	await settle()
	expect(active_map.text == "Granite Orchard — 12 × 10" and badge.text == "Classic" and
		game.get_node("%Phase").text == "Draft · Round 1", "Restart Draft lost the chosen battlefield or Classic.")
	click(new_match.get_global_rect().get_center())
	await settle()
	expect(entries.get_node("granite-orchard").button_pressed, "The dialog forgot the confirmed map choice.")
	expect(entries.get_node("granite-orchard").get_global_rect().intersects(picker.get_node("Scroll").get_global_rect()),
		"The remembered map was outside the visible list.")
	entries.get_node("wrenfield").pressed.emit()
	dialog.get_ok_button().pressed.emit()
	root.size = Vector2i(550, 900)
	await settle()
	expect(active_map.text == "Wrenfield — 8 × 9", "A second explicit map did not load.")
	check_banner(board, Vector2(8, 9), ["0", "0"])
	await capture("battlefield-wrenfield")
	click(new_match.get_global_rect().get_center())
	await settle()
	entries.get_node("random").pressed.emit()
	expect(not preview.visible and caption.text == "Random Map", "Random Map displayed a specific map preview.")
	dialog.get_ok_button().pressed.emit()
	await settle()
	click(new_match.get_global_rect().get_center())
	await settle()
	expect(entries.get_node("random").button_pressed and not preview.visible and
		entries.get_children().slice(1).any(func(entry): return entry.text == active_map.text),
		"The Random preference did not stick or the active battlefield label is missing.")
	dialog.get_cancel_button().pressed.emit()
	game.queue_free()
	await process_frame

func check_map_list_scroll(dialog: Window, scroll: ScrollContainer, entries: VBoxContainer, caption: Label) -> void:
	scroll.scroll_vertical = 0
	await settle()
	var original_caption := caption.text
	var row: Button = entries.get_node("alder-common")
	var visible_row := row.get_global_rect().intersection(scroll.get_global_rect())
	expect(visible_row.has_area(), "The map row must be visible before starting a drag.")
	var start := visible_row.get_center()
	var offset := Vector2(0, -30)
	var emulate := Input.emulate_touch_from_mouse
	Input.emulate_touch_from_mouse = true
	var motion := InputEventMouseMotion.new()
	motion.position = start
	dialog.push_input(motion, true)
	var press := InputEventMouseButton.new()
	press.button_index = MOUSE_BUTTON_LEFT
	press.position = start
	press.pressed = true
	dialog.push_input(press, true)
	motion.position = start + offset
	motion.relative = offset
	motion.button_mask = MOUSE_BUTTON_MASK_LEFT
	dialog.push_input(motion, true)
	press.position = motion.position
	press.pressed = false
	dialog.push_input(press, true)
	Input.emulate_touch_from_mouse = emulate
	await settle()
	expect(scroll.scroll_vertical > 0 and caption.text == original_caption,
		"A drag starting on a map row did not scroll, or selected the row.")
	scroll.scroll_vertical = 0
	await settle()
	var tap := InputEventScreenTouch.new()
	tap.position = start
	tap.pressed = true
	dialog.push_input(tap, true)
	tap.pressed = false
	dialog.push_input(tap, true)
	expect(caption.text == row.text, "A tap on the map row did not update the preview.")

func capture(file_name: String) -> void:
	var directory := OS.get_environment("CASTRUM_M8B_SCREENSHOTS")
	if directory.is_empty():
		return
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	expect(image != null and image.save_png(directory.path_join(file_name + ".png")) == OK,
		"Could not save the %s screenshot." % file_name)

func check_board_extremes() -> void:
	for sample in [["open-meadow", Vector2(6, 5), Vector2(2, 2)], ["twelve-towers", Vector2(12, 12), Vector2(11, 11)]]:
		var dimensions: Vector2 = sample[1]
		var large := dimensions.x == 12
		root.size = Vector2i(360, 640)
		var game := MATCH_SCENE.instantiate()
		game.set("Mode", 1)
		game.set("Rules", 0)
		game.set("MapIdOverride", sample[0])
		root.add_child(game)
		await settle()
		var board: Control = game.get_node("%BoardRenderer")
		check_banner(board, dimensions, ["0", "0"])
		var submit: Button = game.get_node("%Submit")
		var phase: Label = game.get_node("%Phase")
		var one: Button = game.get_node("%PlayerOne")
		var two: Button = game.get_node("%PlayerTwo")
		expect(phase.tooltip_text.ends_with("(%d × %d)" % [dimensions.x, dimensions.y]), "Wrong map size loaded.")
		if large:
			await capture("10c-large-portrait")
		if large:
			choose(board, Vector2(2, 2), dimensions)
			await settle()
			expect(submit.disabled, "An inaccessible tile was selectable.")
		choose(board, Vector2.ZERO, dimensions, true)
		await settle()
		expect(not submit.disabled, "Top-left start did not register on %s." % sample[0])
		root.size = Vector2i(1280, 720)
		await settle()
		check_banner(board, dimensions, ["0", "0"])
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
		var expected_phase := "Draft · Round 2" if large else "Main · Turn 1"
		expect(phase.text == expected_phase, "Opening round did not resolve on %s." % sample[0])
		if large:
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
			await capture("10c-large-landscape")
			if not OS.get_environment("CASTRUM_M8B_SCREENSHOTS").is_empty():
				root.size = Vector2i(360, 640)
				await settle()
				await capture("10c1-compact-main")
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
	var banner: Control = board.get_node("TerritoryScoreBanner")
	await settle()
	check_banner(board, Vector2(7, 5), ["0", "0"])
	choose(board, Vector2(4, 0))
	expect(not submit.disabled, "Draft selection did not enable submit.")
	click(one.get_global_rect().get_center())
	expect(one.button_pressed and not submit.disabled, "Clicking the active player lost its active state or provisional choice.")
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
	choose(board, Vector2(1, 1))
	await settle()
	expect(detail.text == "Waiting for Player Two." and "Submitted" in one.text, "Revision lost the waiting state.")
	expect(board.get_global_rect() == sidebar_rect, "Revision moved the board.")
	await play(board, two, submit, Vector2(3, 2))
	expect(two.button_pressed and phase.text == "Draft · Round 2", "Switching players did not complete the draft round.")

	# Reusing the original E1 target proves the first player submitted the revision to B2.
	await play(board, one, submit, Vector2(4, 0))
	await play(board, two, submit, Vector2(5, 3))
	expect(phase.text == "Main · Turn 1", "The opening did not advance to the main phase.")
	check_banner(board, Vector2(7, 5), ["0", "0"])
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
	for index in remaining_turns.size():
		var turn: Array = remaining_turns[index]
		var prior_area: int = banner.call("GetClaimableArea")
		if index == 3:
			await capture("10c-before-grave")
		if index == 6 and not OS.get_environment("CASTRUM_M8B_SCREENSHOTS").is_empty():
			game.call("SetTemporaryPresentation", true, false, true)
			for sample in 3:
				await capture("10c-motion-%d" % sample)
				await create_timer(0.62).timeout
			game.call("SetTemporaryPresentation", true, true, true)
		await play(board, one, submit, turn[0])
		await play(board, two, submit, turn[1])
		if index == 3:
			print("10C collision changed claimable area from %d to %d." % [prior_area, banner.call("GetClaimableArea")])
			expect(banner.call("GetClaimableArea") < prior_area and piece_art_at(board, turn[0], Vector2(7, 5)) == "grave.png",
				"The authoritative area did not shrink when a collision created a grave.")
			expect(board.call("ReadGroundDamage", 2, 2) > 0 and board.call("ReadDamageMarkerCount", 2, 2) == 0,
				"A Classic grave lost its terrain wear or drew damage markers.")
			await capture("10c-after-grave")
	var heading: Label = game.get_node("%Status")
	expect(phase.text == "Match complete" and heading.text == "Player Two wins", "Missing final outcome.")
	check_banner(board, Vector2(7, 5), ["8", "18"])
	var finished_levels: Vector2 = banner.call("GetDisplayedFill")
	expect(finished_levels.x > 0 and finished_levels.y > finished_levels.x and finished_levels.x + finished_levels.y <= 1.001,
		"The finished banner did not preserve authoritative proportions.")
	await capture("10c-finished")
	expect(restart.text == "Restart Match" and not submit.visible and board.visible, "Incorrect finished controls.")
	check_controls_fit([restart], [heading, phase])
	click(restart.get_global_rect().get_center())
	await settle()
	expect(one.button_pressed and phase.text == "Draft · Round 1" and submit.disabled, "Restart did not restore the opening.")
	check_banner(board, Vector2(7, 5), ["0", "0"])
	expect(banner.call("GetClaimableArea") == 0 and banner.call("GetDisplayedFill") == Vector2.ZERO,
		"Restart left liquid or claimable area from the completed match.")

func check_banner(board: Control, dimensions: Vector2, scores: Array) -> void:
	var banner: Control = board.get_node("TerritoryScoreBanner")
	var grid: Rect2 = board.call("GetRenderedGridBounds")
	var first: Label = banner.get_node("Content/PlayerOneScore")
	var second: Label = banner.get_node("Content/PlayerTwoScore")
	var one_shield: TextureRect = banner.get_node("Content/PlayerOneShield")
	var two_shield: TextureRect = banner.get_node("Content/PlayerTwoShield")
	expect(banner.is_visible_in_tree() and banner.size.y > 0.0, "Territory banner is hidden.")
	expect(is_equal_approx(banner.position.x, grid.position.x) and is_equal_approx(banner.size.x, grid.size.x),
		"Territory banner edges do not match the grid.")
	expect(banner.position.y + banner.size.y < grid.position.y and banner.position.y >= 0.0,
		"Territory banner overlaps the grid or lies outside its region.")
	expect(is_equal_approx(grid.size.x / dimensions.x, grid.size.y / dimensions.y), "The tiles are not square.")
	expect(one_shield.texture == two_shield.texture and one_shield.self_modulate.is_equal_approx(Color("#f06c68"))
		and two_shield.self_modulate.is_equal_approx(Color("#69a4ff")), "Wrong shield silhouette or faction colors.")
	if not scores.is_empty():
		expect([first.text, second.text] == scores, "Unexpected territory score: %s/%s" % [first.text, second.text])

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
	var grid: Rect2 = board.call("GetRenderedGridBounds")
	var point := board.get_global_transform_with_canvas() * (grid.position + (coordinate + Vector2(0.5, 0.5)) * grid.size / dimensions)
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
	expect(game.get_node("%NewMatch").visible and not game.get_node("ConfirmNewMatch/Content/Settings/MapSelection/Preview/DifficultyChoice").visible,
		"Local mode must offer New Match without CPU difficulty.")
	check_banner(board, Vector2(7, 5), ["0", "0"])
	var banner: Control = board.get_node("TerritoryScoreBanner")
	var banner_rect := banner.get_global_rect()
	banner.call("ShowScores", 9, 99)
	await settle()
	banner.call("ShowScores", 100, 144)
	await settle()
	expect(banner.get_global_rect() == banner_rect, "Three-digit scores moved the banner.")
	var left_score: Label = banner.get_node("Content/PlayerOneScore")
	var right_score: Label = banner.get_node("Content/PlayerTwoScore")
	expect(banner_rect.encloses(left_score.get_global_rect()) and banner_rect.encloses(right_score.get_global_rect()),
		"Three-digit scores do not fit inside the banner.")
	banner.call("ShowScores", 0, 0)
	var text_width := floorf(detail.get_parent().size.x)
	if detail.get_combined_maximum_size().x != text_width or status.get_combined_maximum_size().x != text_width:
		push_error("The wrapped labels did not use the available panel width.")
		return false
	if player == "Two":
		click(game.get_node("%PlayerTwo").get_global_rect().get_center())
		await settle()
	if detail.text != "Select a target. Select it again or press Submit.":
		push_error("The opening instruction did not appear.")
		return false

	check_controls_fit([game.get_node("%PlayerOne"), game.get_node("%PlayerTwo"), submit], [detail])
	var original: Array[Rect2] = []
	for control in controls:
		original.append(control.get_global_rect())

	choose(board, Vector2(1, 1))
	await settle()
	if submit.disabled or submit.text != "Submit selection":
		push_error("The first press should highlight the draft target without submitting.")
		return false

	choose(board, Vector2(4, 0))
	if submit.disabled or submit.text != "Submit selection":
		push_error("Pressing a different target should move the highlight without submitting.")
		return false
	choose(board, Vector2(4, 0))
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
