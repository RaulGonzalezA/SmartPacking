# Test Instructions

These instructions apply to projects under `tests`.

- Use xUnit and FluentAssertions, following Arrange / Act / Assert.
- Test observable behavior and public Application results, not implementation
  details.
- Keep recommendation tests deterministic: supply fixed trips and wardrobe
  data; do not depend on current time, weather APIs or a real database.
- Add regression coverage for scoring, clean/available filtering, trip-date
  boundaries and packing-list state where behavior changes.
- Do not access SQLite files, network services or machine-specific paths from
  unit tests. Add a separate integration project when persistent storage needs
  direct coverage.
- Run the narrowest relevant test project first, then the solution test command
  when practical.
