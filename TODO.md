# Bug Fixes

- [x] "Remove" button on blank/new game-persona mapping entry should not be there. It should only appear on existing entries.
- [x] Text in input fields should be left-aligned and vertically centered for better readability.
- [x] The first row in the grid is either missing its top border, or the border is hidden under the header. Ensure consistent border rendering for all rows.
- [x] While in the process of creating a new game-persona mapping, the new entry's row height is inconsistent with existing rows. Ensure uniform row height across all entries.

# Enhancements

- [ ] Add setting to auto-save game-persona mapping list whenever changes are made.
  - [ ] If auto-save is not enabled, prompt user to save changes before exiting the application.
- [ ] Streamline adding game exe names to game-persona mapping list:

  - [ ] Persona field should allow user to type in a custom persona name or select from a dropdown of existing personas.
    - [ ] Implement autocomplete functionality for the persona field to suggest existing personas as the user types.
  - [ ] When a user drags and drops a .exe file onto the application window, automatically add the exe name to the mapping list and prompt the user to select a persona for it.
  - [ ] If the game.exe is already in the mapping list, highlight the existing entry instead of adding a duplicate.
  - [ ] Add a feature to discover games currently running on the system and add them to the mapping list.
    - [ ] Smart detection of running processes likely to be games (e.g. programs running fullscreen or windowed fullscreen, high CPU/GPU usage, etc.).
  - [ ] Provide an option to scan common installation directories for games and suggest adding them to the mapping list.

- [x] Refactor separate console window into a dockable, hidden-by-default "Debug Log" panel within the main application window.
  - [x] Implement a toggle button to show/hide the "Debug Log" panel.
  - [x] Ensure the panel retains its state (visible/hidden) between application sessions.
- [ ] Set the "Clear Credentials" button to execute the deletion after a delay of 10 seconds, and during that delay, change the "Clear Credentials" button to "UNDO CLEAR CREDENTIALS (10...9...8...etc.)" in red lettering, which actually just cancels the deletion request when the user clicks it before the timer runs out.

- [ ] Add option to run as a Windows Service for headless operation.

  - [ ] Include necessary configuration settings to manage service behavior.
  - [ ] The GUI becomes a configuration tool when running as a service.
  - [ ] The Start/Stop buttons in the GUI should control the service state when running as a service.
  - [ ] Quitting the application should not stop the service if it is running as a service.
  - [ ] If the service is running but the GUI is not, if an error occurs, launch the GUI to show the error.

- [ ] Replace "Remove" button with a simple ❌ icon. Only display it on hover over existing entries.

- [ ] Dark/Light Mode support for the entire application.
  - [ ] Implement a toggle switch in the settings to allow users to switch between Dark and Light mode.
  - [ ] Include option for app to follow OS theme automatically.

# Cleanup Tasks

- [ ] Find any unused code related to the old console view or pop-up dialogs and mark it as deprecated for future removal.
- [ ] Update documentation to reflect new UI changes and auto-save feature.

s

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
