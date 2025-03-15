Early pass: Identify top-three FX layers (update in Merge Animation if in replace mode)
  - Merge Animation replace mode needs to be careful not to break RC! (TODO)
Late running pass:
  Inject layer 0: Animate _MMD_NotActive to 1
  If layers 1/2 are not part of the original FX: Insert dummy layers (empty motion)
  Add layer (can this be a dummy layer?):
  - If _MMD_NotActive is 0, drive the original 3 layers off (and back on when 1)
Opt-in: New state _machine_ behavior to control MMD behavior (opt in or opt out)