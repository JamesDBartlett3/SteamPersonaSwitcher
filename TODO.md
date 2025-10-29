# Bug Fixes

- [x] Fix excessive tray notifications relating to minimize/close to tray actions.
- [x] Fix positioning of tray menu so that it appears next to the tray icon instead of in the bottom-right corner of the screen.
- [ ] Fix issue where the app cannot be exited when the "Close to tray" option is enabled.

# Enhancements

## UI Improvements

- Game-Persona Mapping List
  - [ ] Add "Display Name" column to show friendly game names alongside exe names. (e.g. "Elite Dangerous" for "EliteDangerous64.exe", "Foxhole" for "War.exe", etc.)
    - [ ] Allow user to edit the display name in-place.
    - [ ] Use display name in tooltips and logs instead of exe name where appropriate.
- Settings toggles
  - Tray
    - [ ] "Start minimized to tray" should be a sub-setting under "Minimize to tray (instead of taskbar)."
    - [ ] "Close to tray (instead of exiting)" should be a sub-setting under "Minimize to tray (instead of taskbar)."
    - [ ] Add "Start/Stop Service" and "Connect/Disconnect" options to systray context menu.
  - [ ] Add "Auto-save changes to game-persona mapping list" setting.
    - [ ] Add setting to auto-save game-persona mapping list whenever changes are made.
    - [ ] If auto-save is not enabled, prompt user to save changes before exiting the application.
    - [ ] Indicate unsaved changes in the UI (e.g. with an asterisk next to the mapping list title).
  - [ ] Separate settings into columns or sections for better organization (e.g. General Settings, Steam Settings, UI Settings, etc.).
- [x] Replace "Remove" button with a simple ❌ icon. Only display it on hover over existing entries.
- [ ] Dark/Light Mode support for the entire application.
  - [ ] Implement a toggle switch in the settings to allow users to switch between Dark and Light mode.
  - [ ] Include option for app to follow OS theme automatically.
- [ ] Show application status in the title bar (e.g. "Connected", "Disconnected", "Authenticating", etc.).

## UX/Workflow Improvements

- Game-Persona Mapping List
  - [ ] Persona field should allow user to type in a custom persona name or select from a dropdown of existing personas.
    - [ ] Implement autocomplete functionality for the persona field to suggest existing personas as the user types.
  - [ ] When a user drags and drops a .exe file onto the application window, automatically add the exe name to the mapping list and prompt the user to select a persona for it.
  - [ ] If the game.exe is already in the mapping list, highlight the existing entry instead of adding a duplicate.
  - [ ] Add a feature to discover games currently running on the system and add them to the mapping list.
    - [ ] Smart detection of running processes likely to be games (e.g. programs running fullscreen or windowed fullscreen, high CPU/GPU usage, etc.).
  - [ ] Provide an option to scan common installation directories for games and suggest adding them to the mapping list.
  - [ ] When adding a new game-persona mapping and the executable is already running, get the window title of the running process and use it to auto-fill the display name field, but only if it is currently blank.

## Credential Management Improvements

- [ ] Set the "Clear Credentials" button to execute the deletion after a delay of 10 seconds, and during that delay, change the "Clear Credentials" button to "UNDO CLEAR CREDENTIALS (10...9...8...etc.)" in red lettering, which actually just cancels the deletion request when the user clicks it before the timer runs out.
- [ ] If connection to Steam fails and is not able to reconnect after the maximum number of attempts, show a dialog to the user with options to start retrying, edit credentials, or exit the application.

## Back-end Improvements

- [ ] Add option to run as a Windows Service for headless operation.
  - [ ] Include necessary configuration settings to manage service behavior.
  - [ ] The GUI becomes a configuration tool when running as a service.
  - [ ] The Start/Stop buttons in the GUI should control the service state when running as a service.
  - [ ] Quitting the application should not stop the service if it is running as a service.
  - [ ] If the service is running but the GUI is not, if an error occurs, launch the GUI to show the error.
- [ ] Exponentially back off reconnection attempts after repeated failures, up to a maximum delay (e.g., start with 5 seconds, then 10, 30, 60, up to a max of 15 minutes).
- [ ] Support for .NET 9.0

# Cleanup Tasks

- [ ] Remove any deprecated/dead/unreachable/unreferenced code and files.

# Tests to Perform

- [ ] Verify that the "Remove" button only appears on existing entries and not on blank/new entries.
- [ ] Verify that text in input fields is left-aligned and vertically centered.
- [ ] Verify that the row heights are consistent across all entries, including new entries being created.
- [ ] Test the drag-and-drop functionality for adding .exe files to the mapping list.
- [ ] Verify that the "Clear Credentials" button behaves as expected, including the countdown and cancellation functionality.
- [ ] Test running the application as a Windows Service, including starting/stopping the service via the GUI and ensuring the service continues running after quitting the GUI.
- [ ] Test minimize to tray, close to tray, and restore from tray.
- [ ] Test startup behavior. Does it start minimized to tray if that setting is enabled? Does it start on system startup if that setting is enabled?
- [ ] Test auto-save functionality for game-persona mapping list changes.
- [ ] Test the "Debug Log" panel for proper toggling and state retention between sessions.
- [ ] Test the persona field's autocomplete functionality for existing personas.
- [ ] Test the game discovery feature for running processes and common installation directories.
- [ ] Verify that documentation is updated and accurate regarding new features and UI changes.
- [ ] Test the GUI as a configuration tool when running as a Windows Service. Do the Start/Stop buttons control the service state correctly? Does quitting the application not stop the service? Does an error launch the GUI if the service is running but the GUI is not?
- [ ] Test the ❌ icon for removing entries. Does it only appear on hover over existing entries?
- [ ] Test whether the option states are preserved and correctly applied on application restart. When the user change an option, it should take effect immediately and be saved for future sessions.
- [ ] Test whether partially filled new entries are handled correctly. If the user starts entering data into a new entry but does not complete it, ensure that it does not create an invalid entry in the mapping list.
