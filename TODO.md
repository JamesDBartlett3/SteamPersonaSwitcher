# Bug Fixes

- [ ] "Remove" button on blank/new game-persona mapping entry should not be there. It should only appear on existing entries.
- [ ] Text in input fields should be left-aligned and vertically centered for better readability.
- [ ] The first row in the grid is either missing its top border, or the border is hidden under the header. Ensure consistent border rendering for all rows.
- [ ] While in the process of creating a new game-persona mapping, the new entry's row height is inconsistent with existing rows. Ensure uniform row height across all entries.

# Enhancements

- [ ] Persona field should allow user to type in a custom persona name or select from a dropdown of existing personas.
  - [ ] Implement autocomplete functionality for the persona field to suggest existing personas as the user types.
- [ ] Refactor separate console window into a dockable, hidden-by-default "Debug Log" panel within the main application window.
  - [ ] Implement a toggle button to show/hide the "Debug Log" panel.
  - [ ] Ensure the panel retains its state (visible/hidden) between application sessions.
- [ ] Add setting to auto-save game-persona mapping list whenever changes are made.
  - [ ] If auto-save is not enabled, prompt user to save changes before exiting the application.
- [ ] Add option to run as a Windows Service for headless operation.
  - [ ] Include necessary configuration settings to manage service behavior.
  - [ ] The GUI becomes a configuration tool when running as a service.
  - [ ] The Start/Stop buttons in the GUI should control the service state when running as a service.
  - [ ] Quitting the application should not stop the service if it is running as a service.
  - [ ] If the service is running but the GUI is not, if an error occurs, launch the GUI to show the error.
- [ ] Streamline adding game exe names to game-persona mapping list:
  - [ ] When a user drags and drops a .exe file onto the application window, automatically add the exe name to the mapping list and prompt the user to select a persona for it.
  - [ ] If the game.exe is already in the mapping list, highlight the existing entry instead of adding a duplicate.
  - [ ] Add a feature to discover games currently running on the system and add them to the mapping list.
    - [ ] Smart detection of running processes likely to be games (e.g. programs running fullscreen or windowed fullscreen, high CPU/GPU usage, etc.).
  - [ ] Provide an option to scan common installation directories for games and suggest adding them to the mapping list.
- [ ] Replace "Remove" button with a simple ❌ icon. Only display it on hover over existing entries.

# Cleanup Tasks

- [ ] Find any unused code related to the old console view or pop-up dialogs and mark it as deprecated for future removal.
- [ ] Update documentation to reflect new UI changes and auto-save feature.
