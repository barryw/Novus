#!/usr/bin/env python3
"""Build Novus @test suites and run them on AmigaOS through the FS-UAE MCP server."""

from __future__ import annotations

import argparse
import base64
import json
import re
import shutil
import subprocess
import sys
import time
import urllib.request
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
SUITE_ASSETS = {
    "tier12-ptplayer": (
        ROOT / "Novus.Tests/Examples/beep.wav",
        ROOT / "Novus.Tests/Examples/GSLINGER.MOD",
    ),
}
FOUNDATION_SUITES = {
    "foundation-primitives": "Novus.Tests/AmigaRuntime/foundation_primitives.novus",
    "foundation-numeric-extended": "Novus.Tests/AmigaRuntime/foundation_numeric_extended.novus",
    "foundation-control-flow": "Novus.Tests/AmigaRuntime/foundation_control_flow.novus",
    "foundation-functions": "Novus.Tests/AmigaRuntime/foundation_functions.novus",
    "foundation-aggregates": "Novus.Tests/AmigaRuntime/foundation_aggregates.novus",
    "foundation-generics-traits": "Novus.Tests/AmigaRuntime/foundation_generics_traits.novus",
    "foundation-errors-patterns": "Novus.Tests/AmigaRuntime/foundation_errors_patterns.novus",
    "foundation-result-custom-error": "Novus.Tests/AmigaRuntime/foundation_result_custom_error.novus",
    "foundation-ownership": "Novus.Tests/AmigaRuntime/foundation_ownership.novus",
    "foundation-tuple-drop": "Novus.Tests/AmigaRuntime/foundation_tuple_drop.novus",
    "foundation-strings": "Novus.Tests/AmigaRuntime/foundation_strings.novus",
    "foundation-bytes": "Novus.Tests/AmigaRuntime/foundation_bytes.novus",
    "foundation-inline-asm": "Novus.Tests/AmigaRuntime/foundation_inline_asm.novus",
    "foundation-systems": "Novus.Tests/AmigaRuntime/foundation_systems.novus",
    "foundation-modules": "Novus.Tests/AmigaRuntime/foundation_modules",
    "const-fn": "Novus.Tests/Examples/test_const_fn.novus",
    "intrinsics": "Novus.Tests/Examples/test_intrinsics.novus",
    "fixed32": "Novus.Tests/Examples/test_fixed32_asm.novus",
}
FOUNDATION_ALL = {"foundation-all": "Novus.Tests/AmigaRuntime"}
STDLIB_ALL = {"stdlib-all": "Novus/std/tests"}
STDLIB_SUITES = {
    f"stdlib-{path.stem.removeprefix('test_').replace('_', '-')}": str(path.relative_to(ROOT))
    for path in sorted((ROOT / "Novus/std/tests").glob("test_*.novus"))
}
STDLIB_FIXTURE_SUITES = {
    "stdlib-io-blocking": "Novus.Tests/AmigaRuntime/stdlib_io_blocking.novus",
}
TLS_LIVE_SUITE = "stdlib-tls-live"
TLS_SERVER_SOURCE = "Novus.Tests/AmigaRuntime/stdlib_tls_server.novus"
STDLIB_OPTIONAL_SUITES = {
    TLS_LIVE_SUITE: "Novus.Tests/AmigaRuntime/stdlib_tls_peer.novus",
}
FOUNDATION_STANDALONE = {
    name: FOUNDATION_SUITES[name] for name in ("const-fn", "intrinsics", "fixed32")
}
AMIGA_SUITES = {
    "ndk-amiga-lib": "Novus.Tests/AmigaRuntime/ndk_amiga_lib.novus",
    "ndk-exec-lifecycle": "Novus.Tests/AmigaRuntime/ndk_exec_lifecycle.novus",
    "ndk-graphics-dma": "Novus.Tests/AmigaRuntime/ndk_graphics_dma.novus",
    "ndk-graphics-raster": "Novus.Tests/AmigaRuntime/ndk_graphics_raster.novus",
    "ndk-graphics-regions": "Novus.Tests/AmigaRuntime/ndk_graphics_regions.novus",
    "ndk-graphics-blits": "Novus.Tests/AmigaRuntime/ndk_graphics_blits.novus",
    "ndk-graphics-colormap": "Novus.Tests/AmigaRuntime/ndk_graphics_colormap.novus",
    "ndk-graphics-text": "Novus.Tests/AmigaRuntime/ndk_graphics_text.novus",
    "ndk-graphics-display-info": "Novus.Tests/AmigaRuntime/ndk_graphics_display_info.novus",
    "ndk-graphics-rp-attrs": "Novus.Tests/AmigaRuntime/ndk_graphics_rp_attrs.novus",
    "ndk-graphics-view": "Novus.Tests/AmigaRuntime/ndk_graphics_view.novus",
    "ndk-graphics-nodes": "Novus.Tests/AmigaRuntime/ndk_graphics_nodes.novus",
    "ndk-graphics-scale": "Novus.Tests/AmigaRuntime/ndk_graphics_scale.novus",
    "ndk-graphics-pixel-array": "Novus.Tests/AmigaRuntime/ndk_graphics_pixel_array.novus",
    "ndk-graphics-layer-lock": "Novus.Tests/AmigaRuntime/ndk_graphics_layer_lock.novus",
    "ndk-graphics-dbuf": "Novus.Tests/AmigaRuntime/ndk_graphics_dbuf.novus",
    "ndk-graphics-sprite-data": "Novus.Tests/AmigaRuntime/ndk_graphics_sprite_data.novus",
    "ndk-graphics-ext-sprite": "Novus.Tests/AmigaRuntime/ndk_graphics_ext_sprite.novus",
    "ndk-graphics-copper-list": "Novus.Tests/AmigaRuntime/ndk_graphics_copper_list.novus",
    "ndk-graphics-view-pipeline": "Novus.Tests/AmigaRuntime/ndk_graphics_view_pipeline.novus",
    "ndk-graphics-blit-queue": "Novus.Tests/AmigaRuntime/ndk_graphics_blit_queue.novus",
    "ndk-graphics-chip-revision": "Novus.Tests/AmigaRuntime/ndk_graphics_chip_revision.novus",
    "ndk-layers-core": "Novus.Tests/AmigaRuntime/ndk_layers_core.novus",
    "ndk-commodities": "Novus.Tests/AmigaRuntime/ndk_commodities.novus",
    "ndk-iffparse": "Novus.Tests/AmigaRuntime/ndk_iffparse.novus",
    "ndk-icon": "Novus.Tests/AmigaRuntime/ndk_icon.novus",
    "ndk-gadtools": "Novus.Tests/AmigaRuntime/ndk_gadtools.novus",
    "ndk-datatypes": "Novus.Tests/AmigaRuntime/ndk_datatypes.novus",
    "ndk-graphics-gels": "Novus.Tests/AmigaRuntime/ndk_graphics_gels.novus",
    "ndk-graphics-gels-render": "Novus.Tests/AmigaRuntime/ndk_graphics_gels_render.novus",
    "ndk-graphics-gels-animation": "Novus.Tests/AmigaRuntime/ndk_graphics_gels_animation.novus",
    "ndk-graphics-font-list": "Novus.Tests/AmigaRuntime/ndk_graphics_font_list.novus",
    "ndk-graphics-layer-bitmap": "Novus.Tests/AmigaRuntime/ndk_graphics_layer_bitmap.novus",
    "ndk-intuition-screen": "Novus.Tests/AmigaRuntime/ndk_intuition_screen.novus",
    "ndk-intuition-core": "Novus.Tests/AmigaRuntime/ndk_intuition_core.novus",
    "ndk-intuition-drawing": "Novus.Tests/AmigaRuntime/ndk_intuition_drawing.novus",
    "ndk-intuition-gadgets": "Novus.Tests/AmigaRuntime/ndk_intuition_gadgets.novus",
    "ndk-intuition-legacy": "Novus.Tests/AmigaRuntime/ndk_intuition_legacy.novus",
    "ndk-intuition-boopsi": "Novus.Tests/AmigaRuntime/ndk_intuition_boopsi.novus",
    "ndk-utility-core": "Novus.Tests/AmigaRuntime/ndk_utility_core.novus",
    "ndk-ieee-math": "Novus.Tests/AmigaRuntime/ndk_ieee_math.novus",
    "ndk-ffp-math": "Novus.Tests/AmigaRuntime/ndk_ffp_math.novus",
    "ndk-reaction-classes": "Novus.Tests/AmigaRuntime/ndk_reaction_classes.novus",
    "ndk-arexx-class": "Novus.Tests/AmigaRuntime/ndk_arexx_class.novus",
    "ndk-requester-class": "Novus.Tests/AmigaRuntime/ndk_requester_class.novus",
    "ndk-audio-device": "Novus.Tests/AmigaRuntime/ndk_audio_device.novus",
    "ndk-device-lifecycle": "Novus.Tests/AmigaRuntime/ndk_device_lifecycle.novus",
    "ndk-input-device": "Novus.Tests/AmigaRuntime/ndk_input_device.novus",
    "ndk-misc-resource": "Novus.Tests/AmigaRuntime/ndk_misc_resource.novus",
    "ndk-translator": "Novus.Tests/AmigaRuntime/ndk_translator.novus",
    "ndk-dtclass": "Novus.Tests/AmigaRuntime/ndk_dtclass.novus",
    "ndk-keymap": "Novus.Tests/AmigaRuntime/ndk_keymap.novus",
    "ndk-battclock-resource": "Novus.Tests/AmigaRuntime/ndk_battclock_resource.novus",
    "ndk-potgo-resource": "Novus.Tests/AmigaRuntime/ndk_potgo_resource.novus",
    "ndk-battmem-resource": "Novus.Tests/AmigaRuntime/ndk_battmem_resource.novus",
    "ndk-timer-device-math": "Novus.Tests/AmigaRuntime/ndk_timer_device_math.novus",
    "ndk-console-device": "Novus.Tests/AmigaRuntime/ndk_console_device.novus",
    "ndk-colorwheel": "Novus.Tests/AmigaRuntime/ndk_colorwheel.novus",
    "ndk-datebrowser": "Novus.Tests/AmigaRuntime/ndk_datebrowser.novus",
    "ndk-nonvolatile": "Novus.Tests/AmigaRuntime/ndk_nonvolatile.novus",
    "ndk-nonvolatile-bad-name": "Novus.Tests/AmigaRuntime/ndk_nonvolatile_bad_name.novus",
    "ndk-cia-resource": "Novus.Tests/AmigaRuntime/ndk_cia_resource.novus",
    "ndk-disk-resource": "Novus.Tests/AmigaRuntime/ndk_disk_resource.novus",
    "ndk-rexxsyslib": "Novus.Tests/AmigaRuntime/ndk_rexxsyslib.novus",
    "ndk-bullet": "Novus.Tests/AmigaRuntime/ndk_bullet.novus",
    "ndk-diskfont": "Novus.Tests/AmigaRuntime/ndk_diskfont.novus",
    "ndk-ramdrive": "Novus.Tests/AmigaRuntime/ndk_ramdrive.novus",
    "ndk-chooser": "Novus.Tests/AmigaRuntime/ndk_chooser.novus",
    "ndk-clicktab": "Novus.Tests/AmigaRuntime/ndk_clicktab.novus",
    "ndk-radiobutton": "Novus.Tests/AmigaRuntime/ndk_radiobutton.novus",
    "ndk-speedbar": "Novus.Tests/AmigaRuntime/ndk_speedbar.novus",
    "ndk-layout": "Novus.Tests/AmigaRuntime/ndk_layout.novus",
    "ndk-virtual": "Novus.Tests/AmigaRuntime/ndk_virtual.novus",
    "ndk-realtime": "Novus.Tests/AmigaRuntime/ndk_realtime.novus",
    "ndk-lowlevel": "Novus.Tests/AmigaRuntime/ndk_lowlevel.novus",
    "ndk-expansion": "Novus.Tests/AmigaRuntime/ndk_expansion.novus",
    "ndk-amigaguide": "Novus.Tests/AmigaRuntime/ndk_amigaguide.novus",
    "ndk-listbrowser": "Novus.Tests/AmigaRuntime/ndk_listbrowser.novus",
    "ndk-locale": "Novus.Tests/AmigaRuntime/ndk_locale.novus",
    "ndk-workbench": "Novus.Tests/AmigaRuntime/ndk_workbench.novus",
    "ndk-resource": "Novus.Tests/AmigaRuntime/ndk_resource.novus",
    "ndk-asl": "Novus.Tests/AmigaRuntime/ndk_asl.novus",
    "ndk-missing-coverage-fill": "Novus.Tests/AmigaRuntime/ndk_missing_coverage_fill.novus",
    "ndk-dos-buffered-io": "Novus.Tests/AmigaRuntime/ndk_dos_buffered_io.novus",
    "ndk-dos-console": "Novus.Tests/AmigaRuntime/ndk_dos_console.novus",
    "ndk-dos-core": "Novus.Tests/AmigaRuntime/ndk_dos_core.novus",
    "ndk-dos-create-process": "Novus.Tests/AmigaRuntime/ndk_dos_create_process.novus",
    "ndk-dos-argument-parsing": "Novus.Tests/AmigaRuntime/ndk_dos_argument_parsing.novus",
    "ndk-dos-assigns": "Novus.Tests/AmigaRuntime/ndk_dos_assigns.novus",
    "ndk-dos-async-packets": "Novus.Tests/AmigaRuntime/ndk_dos_async_packets.novus",
    "ndk-dos-dates-records": "Novus.Tests/AmigaRuntime/ndk_dos_dates_records.novus",
    "ndk-dos-device-proc": "Novus.Tests/AmigaRuntime/ndk_dos_device_proc.novus",
    "ndk-dos-enumeration": "Novus.Tests/AmigaRuntime/ndk_dos_enumeration.novus",
    "ndk-dos-error-report": "Novus.Tests/AmigaRuntime/ndk_dos_error_report.novus",
    "ndk-dos-filesystem-control": "Novus.Tests/AmigaRuntime/ndk_dos_filesystem_control.novus",
    "ndk-dos-list-read": "Novus.Tests/AmigaRuntime/ndk_dos_list_read.novus",
    "ndk-dos-list-write": "Novus.Tests/AmigaRuntime/ndk_dos_list_write.novus",
    "ndk-dos-links-owner": "Novus.Tests/AmigaRuntime/ndk_dos_links_owner.novus",
    "ndk-dos-notify": "Novus.Tests/AmigaRuntime/ndk_dos_notify.novus",
    "ndk-dos-packets": "Novus.Tests/AmigaRuntime/ndk_dos_packets.novus",
    "ndk-dos-segments": "Novus.Tests/AmigaRuntime/ndk_dos_segments.novus",
    "ndk-dos-shell-execution": "Novus.Tests/AmigaRuntime/ndk_dos_shell_execution.novus",
    "ndk-dos-filesystem": "Novus.Tests/AmigaRuntime/ndk_dos_filesystem.novus",
    "ndk-dos-format-io": "Novus.Tests/AmigaRuntime/ndk_dos_format_io.novus",
    "ndk-dos-process-env": "Novus.Tests/AmigaRuntime/ndk_dos_process_env.novus",
    "ndk-dos-resident-segments": "Novus.Tests/AmigaRuntime/ndk_dos_resident_segments.novus",
    "ndk-dos-text-io": "Novus.Tests/AmigaRuntime/ndk_dos_text_io.novus",
    "ndk-exec-semaphores": "Novus.Tests/Examples/test_semaphore.novus",
    "ndk-exec-intrusive-list": "Novus.Tests/Examples/test_intrusive_list.novus",
    "ndk-exec-priority-list": "Novus.Tests/Examples/test_priority_list.novus",
    "ndk-exec-messages": "Novus.Tests/AmigaRuntime/ndk_exec_messages.novus",
    "ndk-exec-memory-core": "Novus.Tests/AmigaRuntime/ndk_exec_memory_core.novus",
    "ndk-exec-task-port": "Novus.Tests/AmigaRuntime/ndk_exec_task_port.novus",
    "ndk-exec-avl": "Novus.Tests/AmigaRuntime/ndk_exec_avl.novus",
    "block-device-read": "Novus.Tests/AmigaRuntime/block_device_read.novus",
    "dos-device-list": "Novus.Tests/AmigaRuntime/dos_device_list.novus",
    "dos-node-draft": "Novus.Tests/AmigaRuntime/dos_node_draft.novus",
    "embedded-segment": "Novus.Tests/AmigaRuntime/embedded_segment.novus",
    "filesystem-registry": "Novus.Tests/AmigaRuntime/filesystem_registry.novus",
    "interop-ownership": "Novus.Tests/AmigaRuntime/interop_ownership.novus",
    "library-capabilities": "Novus.Tests/AmigaRuntime/library_capabilities.novus",
    "tier12-graphics-shapes": "Novus.Tests/AmigaRuntime/tier12_graphics_shapes.novus",
    "tier12-hardware-helpers": "Novus.Tests/AmigaRuntime/tier12_hardware_helpers.novus",
    "tier12-hardware-legacy": "Novus.Tests/AmigaRuntime/tier12_hardware_legacy.novus",
    "tier12-core-values": "Novus.Tests/AmigaRuntime/tier12_core_values.novus",
    "tier12-ui-controls": "ports/hdpart-novus/tests/a4000/ui_controls_test.novus",
    "tier12-static-gadtools": "Novus.Tests/AmigaRuntime/tier12_static_gadtools.novus",
    "tier12-dos-core": "Novus.Tests/AmigaRuntime/tier12_dos_core.novus",
    "tier12-timers": "Novus.Tests/AmigaRuntime/tier12_timers.novus",
    "tier12-timer-system-async": "Novus.Tests/AmigaRuntime/tier12_timer_system_async.novus",
    "tier12-audio-application": "Novus.Tests/AmigaRuntime/tier12_audio_application.novus",
    "tier12-storage-application": "Novus.Tests/AmigaRuntime/tier12_storage_application.novus",
    "tier12-ui-remaining": "Novus.Tests/AmigaRuntime/tier12_ui_remaining.novus",
    "tier12-hardware-audio": "Novus.Tests/AmigaRuntime/tier12_hardware_audio.novus",
    "tier12-exec-core": "Novus.Tests/AmigaRuntime/tier12_exec_core.novus",
    "tier12-mui": "Novus.Tests/AmigaRuntime/tier12_mui.novus",
    "tier12-ptplayer": "Novus.Tests/AmigaRuntime/tier12_ptplayer.novus",
    "tier12-gels": "Novus.Tests/AmigaRuntime/tier12_gels.novus",
    "tier12-graphics-sprite": "Novus.Tests/AmigaRuntime/tier12_graphics_sprite.novus",
    "tier12-intuition-owners": "Novus.Tests/AmigaRuntime/tier12_intuition_owners.novus",
    "tier12-graphics-patterns": "Novus.Tests/AmigaRuntime/tier12_graphics_patterns.novus",
    "tier12-audio-streaming": "Novus.Tests/AmigaRuntime/tier12_audio_streaming.novus",
    "tier12-dos-file": "Novus.Tests/AmigaRuntime/tier12_dos_file.novus",
    "tier12-resource-bank": "Novus.Tests/AmigaRuntime/tier12_resource_bank.novus",
    "tier12-graphics-contexts": "Novus.Tests/AmigaRuntime/tier12_graphics_contexts.novus",
    "tier12-exec-memory": "Novus.Tests/AmigaRuntime/tier12_exec_memory.novus",
    "tier12-graphics-hardware": "Novus.Tests/AmigaRuntime/tier12_graphics_hardware.novus",
    "tier12-exec-tasks": "Novus.Tests/AmigaRuntime/tier12_exec_tasks.novus",
    "tier12-platform-helpers": "Novus.Tests/AmigaRuntime/tier12_platform_helpers.novus",
    "tier12-reaction": "Novus.Tests/AmigaRuntime/tier12_reaction.novus",
    "tier12-workbench-helpers": "Novus.Tests/AmigaRuntime/tier12_workbench_helpers.novus",
    "tier12-intuition-remaining": "Novus.Tests/AmigaRuntime/tier12_intuition_remaining.novus",
    "tier12-streamable": "Novus.Tests/AmigaRuntime/tier12_streamable.novus",
    "hdpart-core": "ports/hdpart-novus",
    "hdpart-device-scan": "ports/hdpart-novus/tests/a4000/device_scan_test.novus",
    "hdpart-driver-load": "ports/hdpart-novus/tests/a4000/driver_load_test.novus",
    "hdpart-format-handler": "ports/hdpart-novus/tests/a4000/format_handler_test.novus",
    "hdpart-live-discovery": "ports/hdpart-novus/tests/a4000/live_discovery_test.novus",
    "hdpart-live-safety": "ports/hdpart-novus/tests/a4000/live_safety_test.novus",
    "hdpart-rdb-device": "ports/hdpart-novus/tests/a4000/rdb_device_test.novus",
    "hdpart-ui-state": "ports/hdpart-novus/tests/a4000/ui_state_test.novus",
    "hdpart-ui-controls": "ports/hdpart-novus/tests/a4000/ui_controls_test.novus",
    "memory": "Novus/std/tests/test_memory.novus",
    "str": "Novus/std/tests/test_str.novus",
    "string": "Novus/std/tests/test_string.novus",
    "string-builder": "Novus/std/tests/test_string_builder.novus",
    "string-parsing": "Novus/std/tests/test_string_parsing.novus",
    "vec": "Novus/std/tests/test_vec.novus",
    "vecdeque": "Novus/std/tests/test_vecdeque.novus",
    "hashset": "Novus/std/tests/test_hashset.novus",
    "path": "Novus/std/tests/test_path.novus",
    "file-io": "Novus/std/tests/test_file_io.novus",
    "prefs": "Novus/std/tests/test_prefs.novus",
    "window": "Novus/std/tests/test_window.novus",
    "drawing": "Novus/std/tests/test_drawing.novus",
    "async-sleep": "Novus.Tests/AmigaRuntime/async_sleep_failures.novus",
    "result-contracts": "Novus.Tests/AmigaRuntime/result_contract_failures.novus",
    "channel": "Novus.Tests/Examples/channel_comprehensive_test.novus",
}
STDIN_FIXTURES = {"stdlib-io-blocking": b"ab"}
HELD_KEY_FIXTURES = {
    "ndk-input-device": 56,
    "ndk-lowlevel": 0,
}  # macOS virtual key code: left shift
# ramdrive.device exposes KillRAD(unit) and the historical KillRAD0(). Both tear
# a unit down permanently, and unit zero cannot be rebuilt inside one session, so
# each entry point needs a unit of its own. The stock RAD mount entry documents
# extra units as copies carrying a different Unit value; RAD1 is that copy, built
# on the guest so the test owns its fixture instead of depending on the image.
RAD1_MOUNT_ENTRY = (
    "RAD1:",
    "Device = ramdrive.device",
    "Unit = 1",
    "Flags = 0",
    "Surfaces = 2",
    "SectorsPerTrack = 11",
    "SectorSize = 512",
    "Reserved = 2",
    "Interleave = 0",
    "Buffers = 5",
    "BufMemType = 1",
    "LowCyl = 0",
    "HighCyl = 79",
    "#",
)

RAD1_SETUP = tuple(
    f'Echo "{line}" {">" if index == 0 else ">>"}T:RAD1'
    for index, line in enumerate(RAD1_MOUNT_ENTRY)
) + ("C:Mount RAD1: FROM T:RAD1",)

# Mount only adds the DOS entry; the handler starts and the unit claims its RAM on
# first access. Touch both volumes here so that allocation is already accounted for
# when the test samples memory, otherwise the test looks like it leaks a unit's
# worth of RAM and --memory-check reruns it against a unit it has already killed.
RAD_TOUCH = ("C:List RAD:", "C:List RAD1:")

SUITE_SETUP = {"ndk-ramdrive": ("C:Mount RAD:",) + RAD1_SETUP + RAD_TOUCH}
ALL_SUITES = (FOUNDATION_ALL | STDLIB_ALL | STDLIB_SUITES | STDLIB_FIXTURE_SUITES |
              STDLIB_OPTIONAL_SUITES |
              FOUNDATION_SUITES | AMIGA_SUITES)
PROFILES = {
    "debug": (0, 2, False),
    "release-o1": (1, 1, True),
    "release-o3": (3, 1, True),
}
PROCESS_MEMORY_TOLERANCE = 256


class McpError(RuntimeError):
    pass


class McpClient:
    def __init__(self, url: str):
        self.url = url
        self.request_id = 1

    def call(self, name: str, arguments: dict[str, Any], timeout: int = 150) -> Any:
        body = json.dumps({
            "jsonrpc": "2.0",
            "id": self.request_id,
            "method": "tools/call",
            "params": {"name": name, "arguments": arguments},
        }).encode()
        self.request_id += 1
        request = urllib.request.Request(
            self.url,
            data=body,
            headers={"Content-Type": "application/json", "Accept": "application/json"},
        )
        with urllib.request.urlopen(request, timeout=timeout) as response:
            payload = json.load(response)
        if "error" in payload:
            raise McpError(json.dumps(payload["error"], sort_keys=True))
        result = payload.get("result", {})
        text = "\n".join(
            item.get("text", "") for item in result.get("content", [])
            if item.get("type") == "text"
        )
        if result.get("isError"):
            raise McpError(text or "MCP tool failed")
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            return text


class Machine:
    def __init__(self, client: McpClient, configuration: str,
                 hdf: Path | None = None, hdf_drive: int | None = None):
        self.client = client
        self.configuration = configuration
        self.hdf = hdf
        self.hdf_drive = hdf_drive
        self.id: str | None = None

    def _machines_for_configuration(self) -> list[dict[str, Any]]:
        machines = self.client.call("fsuae_machines_list", {})
        return [
            machine for machine in machines
            if machine.get("configuration") == self.configuration
        ]

    def _stop_existing_machine(self, machine_id: str) -> None:
        self.client.call("fsuae_machine_stop", {"machine_id": machine_id})

    def _recover_stale_machines(self) -> list[dict[str, Any]]:
        machines = self._machines_for_configuration()
        stale_machines = [
            machine for machine in machines
            if machine.get("status") != "running" or not machine.get("guest_control_ready")
        ]
        for machine in stale_machines:
            self._stop_existing_machine(machine["machine_id"])
        if stale_machines:
            # Give stale machine stop requests a moment to complete.
            deadline = time.monotonic() + 8.0
            while time.monotonic() < deadline:
                remaining = self._machines_for_configuration()
                stale_machines = [
                    machine for machine in remaining
                    if machine.get("status") != "running" or not machine.get("guest_control_ready")
                ]
                if not stale_machines:
                    break
                time.sleep(0.25)
            else:
                raise McpError(
                    "Timed out waiting for stale FS-UAE machine(s) to stop: " +
                    ", ".join(machine["machine_id"] for machine in stale_machines),
                )
            machines = remaining
        return [
            machine for machine in self._machines_for_configuration()
            if machine.get("status") == "running" and machine.get("guest_control_ready")
        ]

    def start(self) -> None:
        running = self._recover_stale_machines()
        if running:
            raise McpError("Refusing to reuse or stop an existing FS-UAE machine")
        arguments: dict[str, Any] = {"configuration": self.configuration}
        if self.hdf:
            arguments["hdf"] = {"path": str(self.hdf), "read_only": False}
            if self.hdf_drive is not None:
                arguments["hdf"]["drive"] = self.hdf_drive
        result = self.client.call("fsuae_machine_start", arguments)
        match = re.search(r"machine_id ([0-9a-f-]+)", str(result))
        if not match:
            raise McpError(f"Could not parse machine id from: {result}")
        self.id = match.group(1)
        self.wait()

    def wait(self) -> Any:
        assert self.id
        return self.client.call("fsuae_machine_wait", {
            "machine_id": self.id,
            "condition": "workbench",
            "timeout_seconds": 120,
        })

    def diagnostics(self) -> dict[str, Any]:
        assert self.id
        try:
            result = self.client.call("fsuae_machine_diagnostics", {"machine_id": self.id})
            return result if isinstance(result, dict) else {"message": str(result)}
        except Exception as error:
            return {"diagnostics_error": str(error)}

    def recover(self) -> str:
        assert self.id
        try:
            self.client.call("fsuae_machine_reset", {"machine_id": self.id, "hard": True})
            self.wait()
            return "hard_reset"
        except Exception as reset_error:
            previous_id = self.id
            self.stop()
            try:
                self.start()
                return f"restart_after_reset_failure: {reset_error}"
            except Exception as restart_error:
                self.id = previous_id
                return f"recovery_failed: reset={reset_error}; restart={restart_error}"

    def stop(self) -> None:
        if not self.id:
            return
        machine_id, self.id = self.id, None
        try:
            self.client.call("fsuae_machine_stop", {"machine_id": machine_id}, timeout=30)
        except Exception as error:
            print(f"warning: failed to stop {machine_id}: {error}", file=sys.stderr)


def compiler_path(explicit: str | None) -> Path:
    candidates = ([Path(explicit)] if explicit else []) + [
        ROOT / "Novus/bin/Debug/net10.0/Novus.dll",
        ROOT / "Novus/bin/Release/net10.0/Novus.dll",
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError("Build Novus first, or pass --compiler PATH")


def build_suite(
    compiler: Path, build_root: Path, suite: str, source: Path, profile: str,
    test_filter: str | None, benchmark: bool, memory_check: bool, cache_dir: Path,
) -> tuple[Path | None, dict[str, Any]]:
    optimize, safety, release = PROFILES[profile]
    output_dir = build_root / profile / suite
    output_dir.mkdir(parents=True, exist_ok=True)
    for asset in SUITE_ASSETS.get(suite, ()):
        shutil.copy2(asset, output_dir / asset.name)
    command = [
        "dotnet", str(compiler), "test", str(source),
        "-o", str(output_dir), "--cpu", "68020",
        "--safety-level", str(safety), "-O", str(optimize),
        "--cache-dir", str(cache_dir),
    ]
    if release:
        command.append("--release")
    if test_filter:
        command.extend(("--filter", test_filter))
    if benchmark:
        command.append("--benchmark")
    if memory_check:
        command.append("--memory-check")
    started = time.monotonic()
    process = subprocess.run(command, cwd=ROOT, text=True, capture_output=True)
    record = {
        "status": "built" if process.returncode == 0 else "build_failed",
        "seconds": round(time.monotonic() - started, 3),
        "return_code": process.returncode,
    }
    if process.returncode != 0:
        record["stdout"] = process.stdout[-8000:]
        record["stderr"] = process.stderr[-8000:]
        return None, record
    executable = output_dir / "tests"
    if not executable.is_file():
        record.update(status="build_failed", error=f"missing executable: {executable}")
        return None, record
    record["bytes"] = executable.stat().st_size
    return executable, record


def diagnostic_summary(diagnostics: dict[str, Any]) -> str:
    exception = diagnostics.get("cpu_exception") or {}
    alert = diagnostics.get("alert_code") or diagnostics.get("guru") or diagnostics.get("alert")
    if exception:
        return "cpu exception {vector} ({name}) at {faulting_pc}, task {task_name}".format(
            **{key: exception.get(key, "?") for key in
               ("vector", "name", "faulting_pc", "task_name")}
        )
    if alert:
        return f"alert/guru {alert}"
    return str(diagnostics.get("status", "no structured crash data"))


def available_memory(machine: Machine) -> int | None:
    samples = []
    for _ in range(3):
        try:
            result = guest_command(machine, "Avail FLUSH", required=False)
        except Exception:
            continue
        match = re.search(r"(?im)^total\s+(\d+)", result.get("output", ""))
        if match:
            samples.append(int(match.group(1)))
    return max(samples) if samples else None


def run_suite(
    machine: Machine, executable: Path, suite: str, profile: str, timeout: int, index: int
) -> dict[str, Any]:
    assert machine.id
    amiga_name = f"n{index:02x}{profile[-2:].replace('-', '')}"
    machine.client.call("fsuae_exchange_put", {
        "machine_id": machine.id,
        "name": amiga_name,
        "data_base64": base64.b64encode(executable.read_bytes()).decode(),
    })
    output_name = f"{amiga_name}out"
    command = f"MCP:{amiga_name}"
    if fixture := STDIN_FIXTURES.get(suite):
        fixture_name = f"{amiga_name}in"
        machine.client.call("fsuae_exchange_put", {
            "machine_id": machine.id,
            "name": fixture_name,
            "data_base64": base64.b64encode(fixture).decode(),
        })
        command += f" <MCP:{fixture_name}"
    command += f" >MCP:{output_name}"
    started = time.monotonic()
    record: dict[str, Any] = {"suite": suite, "profile": profile}
    try:
        record["memory_before"] = available_memory(machine)
        held_key = HELD_KEY_FIXTURES.get(suite)
        if held_key is not None:
            machine.client.call("fsuae_input", {
                "machine_id": machine.id, "event": "key",
                "code": held_key, "pressed": True,
            })
        try:
            result = machine.client.call("fsuae_command_execute", {
                "machine_id": machine.id,
                "command": command,
                "timeout_seconds": timeout,
            }, timeout=timeout + 15)
        finally:
            if held_key is not None:
                machine.client.call("fsuae_input", {
                    "machine_id": machine.id, "event": "key",
                    "code": held_key, "pressed": False,
                })
        output = result.get("output", "") if isinstance(result, dict) else str(result)
        record["memory_after_command"] = available_memory(machine)
        if record["memory_before"] is not None and record["memory_after_command"] is not None:
            record["memory_delta_command"] = record["memory_after_command"] - record["memory_before"]
        try:
            exchange = machine.client.call("fsuae_exchange_get", {
                "machine_id": machine.id,
                "name": output_name,
            })
            output = base64.b64decode(exchange["data_base64"]).decode("latin-1")
            if isinstance(result, dict):
                result["output"] = output
                result["output_base64"] = exchange["data_base64"]
        except Exception as error:
            record["output_fetch_error"] = str(error)
        guest_command(machine, f"Delete MCP:{output_name} >NIL:", required=False)
        record["memory_after"] = available_memory(machine)
        if record["memory_before"] is not None and record["memory_after"] is not None:
            record["memory_delta"] = record["memory_after"] - record["memory_before"]
        record["result"] = result
        passed = (
            isinstance(result, dict)
            and result.get("status") == "completed"
            and result.get("succeeded") is True
            and result.get("exit_code") == 0
            and "*** ALL TESTS PASSED ***" in output
        )
        record["status"] = "passed" if passed else "failed"
        if not passed:
            diagnostics = machine.diagnostics()
            record["diagnostics"] = diagnostics
            if (
                diagnostics.get("status") in {"guest_crashed", "guest_command_timed_out", "guruing"}
                or diagnostics.get("guest_control_ready") is False
            ):
                record["recovery"] = machine.recover()
    except Exception as error:
        record.update(status="infrastructure_failed", error=str(error))
        diagnostics = machine.diagnostics()
        record["diagnostics"] = diagnostics
        record["recovery"] = machine.recover()
    record["seconds"] = round(time.monotonic() - started, 3)
    return record


def put_file(machine: Machine, name: str, path: Path) -> None:
    assert machine.id
    machine.client.call("fsuae_exchange_put", {
        "machine_id": machine.id,
        "name": name,
        "data_base64": base64.b64encode(path.read_bytes()).decode(),
    })


def guest_command(
    machine: Machine, command: str, timeout: int = 120, required: bool = True,
) -> dict[str, Any]:
    assert machine.id
    result = machine.client.call("fsuae_command_execute", {
        "machine_id": machine.id,
        "command": command,
        "timeout_seconds": timeout,
    }, timeout=timeout + 15)
    if required and (not isinstance(result, dict) or not result.get("succeeded")):
        raise McpError(f"guest command failed: {command}: {result}")
    return result


def disable_patchasl(machine: Machine) -> dict[str, Any]:
    """Remove the per-boot MUI ASL patch so raw asl.library tests hit the OS."""
    status = guest_command(machine, "Status FULL")
    match = re.search(
        r"(?m)^Process\s+(\d+):.*Loaded as command: MUI:PatchASL$",
        status.get("output", ""),
    )
    if not match:
        return {"status": "not_running"}
    process = match.group(1)
    guest_command(machine, f"Break {process} C")
    verify = guest_command(machine, "Status FULL")
    if "Loaded as command: MUI:PatchASL" in verify.get("output", ""):
        raise McpError("PatchASL ignored Ctrl-C; native asl.library tests are not authoritative")
    return {"status": "stopped", "process": int(process)}


def amissl_files(root: Path) -> dict[str, Path]:
    files = {
        "library": root / "Libs/AmigaOS3/AmiSSL/68020-40/amissl_v362.library",
        "master": root / "Libs/AmigaOS3/amisslmaster.library",
    }
    missing = [str(path) for path in files.values() if not path.is_file()]
    if missing:
        raise FileNotFoundError("missing AmiSSL live-test file(s): " + ", ".join(missing))
    return files


def tls_credentials(build_root: Path) -> tuple[Path, Path]:
    directory = build_root / "tls-fixture"
    certificate, private_key = directory / "certificate.pem", directory / "private-key.pem"
    if certificate.is_file() and private_key.is_file():
        return certificate, private_key
    directory.mkdir(parents=True, exist_ok=True)
    process = subprocess.run([
        "openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes",
        "-subj", "/CN=localhost", "-days", "1",
        "-keyout", str(private_key), "-out", str(certificate),
    ], text=True, capture_output=True)
    if process.returncode != 0:
        raise RuntimeError("failed to generate TLS fixture: " + process.stderr.strip())
    return certificate, private_key


def provision_amissl(machine: Machine, root: Path, build_root: Path) -> dict[str, Any]:
    files = amissl_files(root)
    files["certificate"], files["private_key"] = tls_credentials(build_root)
    for name, key in (("namissl", "library"), ("nmaster", "master"),
                      ("ntcert", "certificate"), ("ntkey", "private_key")):
        put_file(machine, name, files[key])
    for command in (
        "MakeDir RAM:NovusAmiSSL",
        "MakeDir RAM:NovusAmiSSL/Libs",
        "MakeDir RAM:NovusAmiSSL/Libs/AmiSSL",
        "Copy MCP:namissl RAM:NovusAmiSSL/Libs/AmiSSL/amissl_v362.library",
        "Copy MCP:nmaster RAM:NovusAmiSSL/Libs/amisslmaster.library",
        "Assign AmiSSL: RAM:NovusAmiSSL",
        "Assign LIBS: RAM:NovusAmiSSL/Libs ADD",
    ):
        guest_command(machine, command)
    version = guest_command(machine, "Version LIBS:amisslmaster.library FULL")
    return {
        "root": str(root),
        "master_version": version.get("output", "").strip(),
        "library_bytes": files["library"].stat().st_size,
        "master_bytes": files["master"].stat().st_size,
    }


def provision_nonvolatile(machine: Machine, volume: str) -> dict[str, Any]:
    root = f"{volume}:"
    storage = f"{root}NovusNVTest"
    for directory in (
        f"{root}Prefs", f"{root}Prefs/Env-Archive", f"{root}Prefs/Env-Archive/Sys",
        storage, "ENV:Sys",
    ):
        guest_command(machine, f"MakeDir {directory}", required=False)
    guest_command(
        machine,
        f'Echo "{storage}" >{root}Prefs/Env-Archive/Sys/nv_location',
    )
    guest_command(machine, f'Echo "{storage}" >ENV:Sys/nv_location')
    return {
        "location": storage,
        "config": guest_command(machine, "Type ENV:Sys/nv_location").get("output", "").strip(),
    }


def cleanup_nonvolatile(machine: Machine, volume: str) -> dict[str, Any]:
    config = guest_command(machine, "Delete ENV:Sys/nv_location QUIET", required=False)
    storage = guest_command(machine, f"Delete {volume}:NovusNVTest ALL QUIET", required=False)
    location = guest_command(
        machine, f"Delete {volume}:Prefs/Env-Archive/Sys/nv_location QUIET", required=False,
    )
    return {
        "config_removed": config.get("succeeded"),
        "location_removed": location.get("succeeded"),
        "storage_removed": storage.get("succeeded"),
    }


def wait_for_marker(machine: Machine, path: str, timeout: int) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        result = guest_command(machine, f"Type {path}", required=False)
        if result.get("succeeded"):
            return result
        time.sleep(0.5)
    raise McpError(f"timed out waiting for {path}")


def run_tls_suite(
    machine: Machine, peer: Path, server: Path, profile: str, timeout: int, index: int,
) -> dict[str, Any]:
    started = time.monotonic()
    record: dict[str, Any] = {"suite": TLS_LIVE_SUITE, "profile": profile}
    peer_name, server_name = f"ntp{index:x}", f"nts{index:x}"
    try:
        put_file(machine, peer_name, peer)
        put_file(machine, server_name, server)
        guest_command(machine, "Delete RAM:novus-tls-ready RAM:novus-tls-passed QUIET", required=False)
        guest_command(machine, f"Run >RAM:novus-tls-server.out MCP:{server_name}")
        ready = wait_for_marker(machine, "RAM:novus-tls-ready", timeout)
        peer_result = guest_command(machine, f"MCP:{peer_name}", timeout)
        passed_marker = wait_for_marker(machine, "RAM:novus-tls-passed", timeout)
        status = guest_command(machine, "Status")
        output = peer_result.get("output", "")
        passed = (
            "ok" in ready.get("output", "")
            and "*** ALL TESTS PASSED ***" in output
            and "ok" in passed_marker.get("output", "")
            and f"MCP:{server_name}" not in status.get("output", "")
        )
        record["result"] = {
            "peer": peer_result,
            "server_ready": ready,
            "server_passed": passed_marker,
            "status": status,
        }
        record["status"] = "passed" if passed else "failed"
        if not passed:
            record["diagnostics"] = machine.diagnostics()
    except Exception as error:
        record.update(status="infrastructure_failed", error=str(error))
        record["ram"] = guest_command(machine, "List RAM:", required=False)
        diagnostics = machine.diagnostics()
        record["diagnostics"] = diagnostics
        if (
            diagnostics.get("status") in {"guest_crashed", "guest_command_timed_out", "guruing"}
            or diagnostics.get("guest_control_ready") is False
        ):
            record["recovery"] = machine.recover()
    record["seconds"] = round(time.monotonic() - started, 3)
    return record


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mcp-url", default="http://localhost:6800/mcp")
    parser.add_argument("--configuration", default="A4000")
    parser.add_argument("--hdf", type=Path,
                        help="attach a writable disposable HDF without changing the saved configuration")
    parser.add_argument("--hdf-drive", type=int,
                        help="DH slot for --hdf; defaults to the first unused slot")
    parser.add_argument("--nonvolatile-volume",
                        help="mounted disposable volume used by ndk-nonvolatile (for example NDK0)")
    parser.add_argument("--compiler")
    parser.add_argument("--build-dir", type=Path,
                        default=ROOT / ".novus-cache/amiga-runtime-suite")
    parser.add_argument("--profile", action="append", choices=PROFILES,
                        help="repeat for a matrix; default: release-o1")
    parser.add_argument("--layer", choices=("foundation", "stdlib", "amiga", "all"),
                        default="foundation", help="default suite layer")
    parser.add_argument("--suite", action="append", choices=ALL_SUITES,
                        help="repeat to select explicit suites")
    parser.add_argument("--timeout", type=int, default=None,
                        help="seconds allowed for each Amiga test executable")
    parser.add_argument("--filter", help="test-name filter passed to novus test")
    parser.add_argument("--benchmark", action="store_true",
                        help="record per-test runtime on the guest")
    parser.add_argument("--memory-check", action="store_true",
                        help="fail tests that leak Novus allocations or AmigaOS memory")
    parser.add_argument("--amissl-dir", type=Path,
                        help="extracted AmiSSL v5 AmiSSL/ directory for stdlib-tls-live")
    parser.add_argument("--list", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.timeout is None:
        args.timeout = 300 if args.configuration.upper().startswith("A1200") else 120
    if args.list:
        for name, path in ALL_SUITES.items():
            layer = "foundation" if name in FOUNDATION_ALL or name in FOUNDATION_SUITES else \
                "stdlib" if name in STDLIB_ALL or name in STDLIB_SUITES or name in STDLIB_FIXTURE_SUITES or name in STDLIB_OPTIONAL_SUITES else "amiga"
            print(f"{layer:10} {name:28} {path}")
        return 0
    profiles = args.profile or ["release-o1"]
    if args.hdf:
        args.hdf = args.hdf.resolve()
        if not args.hdf.is_file():
            print(f"HDF not found: {args.hdf}", file=sys.stderr)
            return 2
    layer_suites = {
        "foundation": FOUNDATION_ALL | FOUNDATION_STANDALONE,
        "stdlib": STDLIB_ALL | STDLIB_FIXTURE_SUITES,
        "amiga": AMIGA_SUITES,
        "all": FOUNDATION_ALL | STDLIB_ALL | STDLIB_FIXTURE_SUITES | FOUNDATION_STANDALONE | AMIGA_SUITES,
    }
    suites = args.suite or list(layer_suites[args.layer])
    needs_nonvolatile = any(suite.startswith("ndk-nonvolatile") for suite in suites)
    if needs_nonvolatile and (
        args.hdf is None or args.hdf_drive is None or not args.nonvolatile_volume
    ):
        print("ndk-nonvolatile requires --hdf PATH --hdf-drive N --nonvolatile-volume NAME", file=sys.stderr)
        return 2
    if TLS_LIVE_SUITE in suites:
        if args.amissl_dir is None:
            print("stdlib-tls-live requires --amissl-dir PATH", file=sys.stderr)
            return 2
        try:
            amissl_files(args.amissl_dir)
        except FileNotFoundError as error:
            print(error, file=sys.stderr)
            return 2
    compiler = compiler_path(args.compiler)
    args.build_dir.mkdir(parents=True, exist_ok=True)
    report: dict[str, Any] = {
        "configuration": args.configuration,
        "compiler": str(compiler),
        "profiles": profiles,
        "benchmark": args.benchmark,
        "memory_check": args.memory_check,
        "hdf": str(args.hdf) if args.hdf else None,
        "hdf_drive": args.hdf_drive,
        "nonvolatile_volume": args.nonvolatile_volume,
        "tests": [],
    }

    builds: list[tuple[str, str, Path, Path | None]] = []
    for profile in profiles:
        for suite in suites:
            print(f"BUILD {profile:10} {suite}...", end=" ", flush=True)
            executable, build = build_suite(
                compiler, args.build_dir, suite, ROOT / ALL_SUITES[suite], profile,
                args.filter, args.benchmark, args.memory_check,
                args.build_dir / ".shared-cache" / profile,
            )
            build.update(suite=suite, profile=profile, source=ALL_SUITES[suite])
            report["tests"].append({"build": build})
            print(f"{build['status']} ({build['seconds']}s)")
            server_executable = None
            if executable and suite == TLS_LIVE_SUITE:
                print(f"BUILD {profile:10} {suite}-server...", end=" ", flush=True)
                server_executable, server_build = build_suite(
                    compiler, args.build_dir, f"{suite}-server",
                    ROOT / TLS_SERVER_SOURCE, profile, args.filter, False, args.memory_check,
                    args.build_dir / ".shared-cache" / profile,
                )
                server_build.update(suite=f"{suite}-server", profile=profile,
                                    source=TLS_SERVER_SOURCE)
                report["tests"].append({"build": server_build})
                print(f"{server_build['status']} ({server_build['seconds']}s)")
            if executable and (suite != TLS_LIVE_SUITE or server_executable):
                builds.append((profile, suite, executable, server_executable))

    machine = Machine(McpClient(args.mcp_url), args.configuration, args.hdf, args.hdf_drive)
    nonvolatile_volume = args.nonvolatile_volume or ""
    try:
        machine.start()
        if TLS_LIVE_SUITE in suites:
            report["amissl"] = provision_amissl(machine, args.amissl_dir, args.build_dir)
        if needs_nonvolatile:
            report["nonvolatile"] = provision_nonvolatile(machine, nonvolatile_volume)
        for index, (profile, suite, executable, server_executable) in enumerate(builds):
            print(f"RUN   {profile:10} {suite}...", end=" ", flush=True)
            if suite == "ndk-asl":
                report["asl_native_setup"] = disable_patchasl(machine)
            for command in SUITE_SETUP.get(suite, ()):
                guest_command(machine, command)
            result = run_tls_suite(
                machine, executable, server_executable, profile, args.timeout, index,
            ) if server_executable else run_suite(
                machine, executable, suite, profile, args.timeout, index,
            )
            if (args.memory_check and not server_executable and result.get("status") == "passed" and
                    result.get("memory_delta", 0) < -PROCESS_MEMORY_TOLERANCE):
                # Reuse the guest command name so confirmations measure the same
                # process lifecycle instead of cold-loading a new DOS command.
                confirmation = run_suite(
                    machine, executable, suite, profile, args.timeout, index,
                )
                result["memory_confirmation"] = {
                    key: confirmation.get(key) for key in (
                        "memory_before", "memory_after_command", "memory_delta_command",
                        "memory_after", "memory_delta", "status",
                    )
                }
                if confirmation.get("status") != "passed":
                    result["status"] = "failed"
                    result["memory_confirmation_failure"] = {
                        key: confirmation.get(key) for key in
                        ("error", "result", "diagnostics") if confirmation.get(key) is not None
                    }
                elif confirmation.get("memory_delta", 0) < -PROCESS_MEMORY_TOLERANCE:
                    final_confirmation = run_suite(
                        machine, executable, suite, profile, args.timeout, index,
                    )
                    result["memory_confirmation_2"] = {
                        key: final_confirmation.get(key) for key in (
                            "memory_before", "memory_after_command", "memory_delta_command",
                            "memory_after", "memory_delta", "status",
                        )
                    }
                    if final_confirmation.get("status") != "passed":
                        result["status"] = "failed"
                        result["memory_confirmation_failure"] = {
                            key: final_confirmation.get(key) for key in
                            ("error", "result", "diagnostics")
                            if final_confirmation.get(key) is not None
                        }
                    elif final_confirmation.get("memory_delta", 0) < -PROCESS_MEMORY_TOLERANCE:
                        result["status"] = "failed"
                        result["process_memory_leak"] = -final_confirmation["memory_delta"]
            report["tests"].append({"run": result})
            detail = ""
            if "diagnostics" in result:
                detail = f" — {diagnostic_summary(result['diagnostics'])}"
            if "process_memory_leak" in result:
                detail = f" — repeatable process teardown leak: {result['process_memory_leak']} bytes"
            elif "memory_confirmation_failure" in result:
                detail = " — memory confirmation behavior failed"
            print(f"{result['status']} ({result['seconds']}s){detail}")
    except Exception as error:
        report["infrastructure_error"] = str(error)
    finally:
        if needs_nonvolatile and machine.id:
            try:
                report["nonvolatile_cleanup"] = cleanup_nonvolatile(machine, nonvolatile_volume)
            except Exception as error:
                report["nonvolatile_cleanup"] = {"error": str(error)}
        machine.stop()

    report_path = args.build_dir / "report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    failures = [
        item for item in report["tests"]
        if next(iter(item.values())).get("status") not in {"built", "passed"}
    ]
    failure_count = len(failures) + int("infrastructure_error" in report)
    print(f"\nReport: {report_path}")
    print(f"Result: {len(report['tests']) - len(failures)} passed records, {failure_count} failed")
    return 1 if failure_count else 0


if __name__ == "__main__":
    raise SystemExit(main())
