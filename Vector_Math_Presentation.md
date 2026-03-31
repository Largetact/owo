# Vector Mathematics in BonelabUtilityMod

### 3 Features Using Vectors, Dot Product, and Cross Product

---

# Feature 1: Source Engine Air Acceleration

## Vector Operation: **Dot Product**

---

## What It Does

The Source/Quake air strafe system lets players **build speed mid-air** by combining strafe keys with camera rotation. The **dot product** determines how much the player can still accelerate in their desired direction.

## Why Dot Product?

The dot product measures **how aligned** two vectors are:

- `Dot(V, W) = |V| × |W| × cos(θ)`
- If velocity **V** is already moving in the wish direction **W**, the dot product is large → little acceleration allowed
- If **V** is perpendicular to **W** (strafing at 90°), dot product is small → maximum acceleration allowed

This creates the classic "strafe to gain speed" mechanic — you gain the most speed when your velocity is sideways to your wish direction.

---

## The Code

```csharp
// From BunnyHopController.cs → ApplyAirStrafeSource()

Vector3 wishDir = (camForward * input.y + camRight * input.x).normalized;
float wishSpeed = 30f; // SV_MAXAIRSPEED

// DOT PRODUCT: How fast are we already going in the wish direction?
float currentSpeedInWishDir = Vector3.Dot(horizontalVel, wishDir);

float addSpeed = wishSpeed - currentSpeedInWishDir;
if (addSpeed <= 0f) return; // Already at max in this direction

float accelSpeed = accel * Time.deltaTime * wishSpeed;
if (accelSpeed > addSpeed) accelSpeed = addSpeed;

// Add velocity in wish direction
Vector3 newVelocity = horizontalVel + wishDir * accelSpeed;
```

---

## Example Calculation

**Given:**

- Player velocity: `V = (10, 0, 0)` (moving right at 10 m/s)
- Wish direction: `W = (0.707, 0, 0.707)` (forward-right at 45°, normalized)
- wishSpeed = 30 m/s, accel = 10, dt = 0.016s (60 FPS)

**Step 1: Dot Product**

```
currentSpeedInWishDir = Dot(V, W)
= (10 × 0.707) + (0 × 0) + (0 × 0.707)
= 7.07 m/s
```

**Step 2: How much can we add?**

```
addSpeed = 30 - 7.07 = 22.93 m/s
```

**Step 3: Frame acceleration**

```
accelSpeed = 10 × 0.016 × 30 = 4.8 m/s
4.8 < 22.93, so accelSpeed = 4.8
```

**Step 4: New velocity**

```
newVelocity = (10, 0, 0) + (0.707, 0, 0.707) × 4.8
= (10, 0, 0) + (3.39, 0, 3.39)
= (13.39, 0, 3.39)
|newVelocity| = √(13.39² + 3.39²) = √(179.3 + 11.5) = √190.8 ≈ 13.81 m/s
```

**Result:** Player went from 10 m/s (purely right) to 13.81 m/s (forward-right). Speed increased by 3.81 m/s in one frame just by strafing!

---

## Flowchart

```
┌─────────────────────────────────┐
│   Start: Player is Airborne     │
└──────────────┬──────────────────┘
               │
┌──────────────▼──────────────────┐
│  Get horizontal velocity V      │
│  Get wish direction W from      │
│  camera + thumbstick            │
└──────────────┬──────────────────┘
               │
┌──────────────▼──────────────────┐
│  currentSpeed = Dot(V, W)       │
│  addSpeed = wishSpeed - current │
└──────────────┬──────────────────┘
               │
         ┌─────▼─────┐
         │addSpeed > 0│
         └─────┬─────┘
          NO   │   YES
    ┌──────┐   │   ┌───────────────────────┐
    │ Keep │   └──►│accelSpeed = accel×dt×ws│
    │  vel │       └───────────┬───────────┘
    └──────┘                   │
                     ┌─────────▼─────────┐
                     │ Cap accelSpeed at  │
                     │ addSpeed if needed  │
                     └─────────┬─────────┘
                               │
                     ┌─────────▼─────────┐
                     │newVel = V + W×aSpd │
                     └─────────┬─────────┘
                               │
                   ┌───────────▼───────────┐
                   │Clamp to maxSpeed if   │
                   │needed, apply to player│
                   └───────────────────────┘
```

---

---

# Feature 2: Surf Ramp Detection

## Vector Operation: **Surface Normal (Implicit Dot Product)**

---

## What It Does

Detects whether the player is standing on a **surfable slope** vs flat ground. On surf ramps, the player is treated as airborne — speed is preserved and air strafing is enabled, allowing smooth surfing like in Source engine.

## Why Surface Normal / Dot Product?

A **surface normal** is a unit vector pointing perpendicular to a surface. Its **Y component** is the implicit dot product with the world up vector:

- `normal.y = Dot(normal, Vector3.up) = cos(θ)` where θ is the angle from vertical
- Flat ground: `normal = (0, 1, 0)` → `normal.y = 1.0` (0° from vertical)
- 45° slope: `normal = (0.707, 0.707, 0)` → `normal.y = 0.707`
- Vertical wall: `normal = (1, 0, 0)` → `normal.y = 0.0` (90° from vertical)

The **standable normal** threshold (default 0.7 ≈ 45°) separates walkable ground from surf ramps.

---

## The Code

```csharp
// From BunnyHopController.cs → CheckSurfRamp()

// Raycast straight down from pelvis
if (Physics.Raycast(pelvis.position, Vector3.down, out RaycastHit hit, 3f))
{
    // hit.normal.y IS the dot product with Vector3.up
    // Flat ground = 1.0, vertical wall = 0.0
    // If normal.y < standable threshold → it's a surf ramp
    return hit.normal.y < _standableNormal;  // default 0.7
}
```

---

## Example Calculation

**Given:**

- Player standing on a slope tilted 50° from horizontal
- Slope angle from vertical = 40°
- standableNormal = 0.7

**Step 1: Surface Normal**
The normal points perpendicular to the surface. For a 50° slope from horizontal:

```
normal = (sin(50°), cos(50°), 0)
       = (0.766, 0.643, 0)
```

**Step 2: Dot Product with Up**

```
normal.y = 0.643
(This equals Dot(normal, (0,1,0)) = cos(40°) = 0.643)
```

**Step 3: Compare with Threshold**

```
0.643 < 0.7?  → YES
```

**Result:** This is a **surf ramp**! Player keeps their speed and can air strafe on it.

**Counter-example:** A 30° slope from horizontal:

```
normal.y = cos(60°) = 0.5... wait
normal = (sin(30°), cos(30°), 0) = (0.5, 0.866, 0)
normal.y = 0.866
0.866 < 0.7?  → NO → This is walkable ground
```

---

## Flowchart

```
┌──────────────────────────────────┐
│  Start: Player touches surface   │
└──────────────┬───────────────────┘
               │
┌──────────────▼───────────────────┐
│  Raycast DOWN from pelvis        │
│  (Vector3.down, max distance 3m) │
└──────────────┬───────────────────┘
               │
         ┌─────▼──────┐
         │ Hit surface?│
         └─────┬──────┘
          NO   │   YES
    ┌──────┐   │   ┌─────────────────────┐
    │ Not  │   └──►│ Get surface normal N │
    │ surf │       │ Extract N.y          │
    └──────┘       │ (= Dot(N, Up))       │
                   └──────────┬──────────┘
                              │
                   ┌──────────▼──────────┐
                   │ N.y < standable     │
                   │ Normal (0.7)?       │
                   └──────────┬──────────┘
                        YES   │   NO
              ┌───────────┐   │   ┌───────────────┐
              │ SURF RAMP │   └──►│WALKABLE GROUND│
              │ • Keep     │       │ • Reset speed │
              │   speed   │       │ • Allow hop   │
              │ • Enable  │       └───────────────┘
              │   strafe  │
              └───────────┘
```

---

---

# Feature 3: Dash Matrix Spawning

## Vector Operation: **Cross Product**

---

## What It Does

When the player dashes, visual effects (explosions, particles) spawn in a **matrix pattern** — a grid, circle, or line — oriented perpendicular to the dash direction. The **cross product** constructs a local coordinate system (right + up axes) from the dash direction.

## Why Cross Product?

The cross product produces a vector **perpendicular** to two input vectors:

- `Cross(A, B) = |A| × |B| × sin(θ) × n̂` (where n̂ is the perpendicular direction)
- `Cross(Up, Forward)` → gives the **right** axis
- `Cross(Forward, Right)` → gives the **up** axis
- Together: an **orthonormal basis** (forward, right, up) — a local coordinate frame aligned with the dash

Without the cross product, we'd have no way to know which direction is "right" or "up" relative to an arbitrary dash direction.

---

## The Code

```csharp
// From DashController.cs → SpawnWithMatrix()

// Step 1: Normalize dash direction
Vector3 forward = direction.normalized;

// Step 2: CROSS PRODUCT → Get right axis perpendicular to dash
Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

// Handle edge case: dashing straight up/down
if (right.sqrMagnitude < 0.01f)
    right = Vector3.right;

// Step 3: CROSS PRODUCT → Get up axis perpendicular to both
Vector3 up = Vector3.Cross(forward, right).normalized;

// Step 4: Use (right, up) basis to place effects in patterns
var offsets = CalculateMatrixOffsets(count, spacing, right, up, mode);
foreach (var offset in offsets)
    SpawnEffect(position + offset);
```

---

## Example Calculation

**Given:**

- Dash direction: `D = (3, 0, 4)` (forward-right, not normalized)

**Step 1: Normalize**

```
|D| = √(3² + 0² + 4²) = √(9 + 16) = √25 = 5
forward = D / |D| = (3/5, 0, 4/5) = (0.6, 0, 0.8)
```

**Step 2: Cross Product → Right Axis**

```
right = Cross(Up, forward) = Cross((0,1,0), (0.6, 0, 0.8))

Cross formula:
  x = (1×0.8) - (0×0)   = 0.8
  y = (0×0.6) - (0×0.8)  = 0
  z = (0×0)   - (1×0.6)  = -0.6

right = (0.8, 0, -0.6)
|right| = √(0.64 + 0 + 0.36) = √1.0 = 1.0  (already unit length!)
```

**Step 3: Cross Product → Up Axis**

```
up = Cross(forward, right) = Cross((0.6, 0, 0.8), (0.8, 0, -0.6))

  x = (0×(-0.6)) - (0.8×0)    = 0
  y = (0.8×0.8)  - (0.6×(-0.6)) = 0.64 + 0.36 = 1.0
  z = (0.6×0)    - (0×0.8)    = 0

up = (0, 1, 0)
```

**Result:** Orthonormal basis:

- `forward = (0.6, 0, 0.8)` — dash direction
- `right   = (0.8, 0, -0.6)` — perpendicular on ground plane
- `up      = (0, 1, 0)` — straight up

For a SQUARE 3×3 matrix with spacing 2m, effects spawn at:

```
(-2, -2), (-2, 0), (-2, 2),
( 0, -2), ( 0, 0), ( 0, 2),  ← (right offset, up offset)
( 2, -2), ( 2, 0), ( 2, 2)

World positions = dashPos + right×rOffset + up×uOffset
e.g. top-right: dashPos + (0.8,0,-0.6)×2 + (0,1,0)×2
              = dashPos + (1.6, 2, -1.2)
```

---

## Flowchart

```
┌──────────────────────────────────┐
│  Start: Player triggers Dash     │
└──────────────┬───────────────────┘
               │
┌──────────────▼───────────────────┐
│  Get dash direction D            │
│  forward = D / ‖D‖  (normalize) │
└──────────────┬───────────────────┘
               │
┌──────────────▼───────────────────┐
│  CROSS PRODUCT #1:               │
│  right = Cross(Up, forward)      │
└──────────────┬───────────────────┘
               │
        ┌──────▼──────────┐
        │‖right‖ ≈ 0?     │
        │(dashing straight│
        │ up or down)      │
        └──────┬──────────┘
         YES   │   NO
  ┌────────┐   │   ┌─────────────┐
  │Fallback│   └──►│Normalize    │
  │right = │       │right vector │
  │(1,0,0) │       └──────┬──────┘
  └───┬────┘              │
      └───────────┬───────┘
                  │
    ┌─────────────▼─────────────┐
    │  CROSS PRODUCT #2:        │
    │  up = Cross(forward, right)│
    └─────────────┬─────────────┘
                  │
    ┌─────────────▼─────────────┐
    │  Orthonormal basis ready:  │
    │  (forward, right, up)      │
    └─────────────┬─────────────┘
                  │
         ┌────────▼────────┐
         │  Matrix Mode?   │
         └────┬──┬──┬──────┘
     CIRCLE   │  │  │  LINE
    ┌─────┐   │  │  │  ┌──────┐
    │sin/ │   │  │  └─►│Along │
    │cos× │   │  │     │right │
    │right│   │  │     │axis  │
    │& up │   │  │     └──┬───┘
    └──┬──┘   │  │  SQUARE│
       │      │  │  ┌─────▼────┐
       │      │  └─►│Grid using│
       │      │     │right×col │
       │      │     │+ up×row  │
       │      │     └────┬─────┘
       └──────┴──────────┘
                  │
    ┌─────────────▼─────────────┐
    │  Spawn effects at each     │
    │  position + offset         │
    └───────────────────────────┘
```

---

---

# Summary

| Feature                 | Vector Operation                      | Purpose                                                     | Why This Operation?                                                                               |
| ----------------------- | ------------------------------------- | ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| **Source Air Accel**    | Dot Product                           | Determine how much speed the player can gain in a direction | Measures alignment between velocity and wish direction — perpendicular = maximum gain             |
| **Surf Ramp Detection** | Surface Normal (implicit Dot with Up) | Classify surfaces as walkable or surfable                   | The Y component of the normal equals cos(angle from vertical), directly comparable to a threshold |
| **Dash Matrix Spawn**   | Cross Product (×2)                    | Build a local coordinate frame from an arbitrary direction  | Cross product uniquely produces a perpendicular axis, enabling 2D patterns oriented to the dash   |

---

# Key Takeaways

1. **Dot Product** → measures **alignment** (how much of one vector goes in another's direction)
   - Range: -1 (opposite) to +1 (same direction) for unit vectors
   - Used for: speed projections, angle checks, visibility tests

2. **Surface Normal** → a unit vector perpendicular to a surface
   - `normal.y` = implicit dot product with world up = `cos(slope_angle)`
   - Used for: ground detection, slope classification, physics responses

3. **Cross Product** → produces a vector **perpendicular** to two inputs
   - Follows the right-hand rule
   - Used for: building coordinate frames, finding rotation axes, computing areas
