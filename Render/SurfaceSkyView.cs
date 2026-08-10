using Godot;
using SolarSim.Bridge;
using SolarSim.Engine.Core;

namespace SolarSim.Render;

/// <summary>
/// Céu da Terra: câmera perspectiva na origem, marcadores numa esfera de raio fixo.
/// Não usa <see cref="ScaleMapper"/> — só direções ENU do <see cref="LocalSky"/>.
/// </summary>
public partial class SurfaceSkyView : Node3D
{
    /// <summary>Raio da esfera celeste em unidades de cena (não km).</summary>
    public const float CelestialRadius = 1000.0f;

    private const float LookSensitivity = 0.004f;

    private ISurfaceSkySource? _source;
    private Camera3D _camera = null!;
    private Node3D _markers = null!;
    private Label3D _hint = null!;
    private readonly Dictionary<string, MeshInstance3D> _meshes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label3D> _labels = new(StringComparer.Ordinal);
    private bool _active;
    private bool _looking;
    private float _yaw;
    private float _pitch = 0.35f;

    public bool IsActive => _active;

    public void Attach(ISurfaceSkySource source, string hintText)
    {
        _source = source;
        if (_hint is not null)
        {
            _hint.Text = hintText;
        }
        else
        {
            SetMeta("pending_hint", hintText);
        }
    }

    public override void _Ready()
    {
        Visible = false;

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.05f, 0.05f, 0.08f),
            },
        });

        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(40.0f, 40.0f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.12f, 0.18f, 0.10f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        ground.Position = new Vector3(0.0f, -1.6f, 0.0f);
        AddChild(ground);

        _markers = new Node3D { Name = "SkyMarkers" };
        AddChild(_markers);

        _camera = new Camera3D
        {
            Name = "SurfaceSkyCamera",
            Projection = Camera3D.ProjectionType.Perspective,
            Fov = 70.0f,
            Near = 0.1f,
            Far = CelestialRadius * 3.0f,
            Current = false,
        };
        AddChild(_camera);

        var hint = HasMeta("pending_hint")
            ? GetMeta("pending_hint").AsString()
            : "Céu da Terra — arraste com o direito";
        _hint = new Label3D
        {
            Text = hint,
            FontSize = 28,
            Modulate = new Color(0.85f, 0.9f, 1.0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(0.0f, 2.2f, -8.0f),
        };
        AddChild(_hint);

        ApplyLook();
    }

    public void SetActive(bool active)
    {
        _active = active;
        Visible = active;
        _camera.Current = active;

        if (active)
        {
            AimAtSunOrSouth();
            ApplyLook();
            RefreshMarkers();
        }
        else
        {
            ClearMarkers();
        }
    }

    private void AimAtSunOrSouth()
    {
        _yaw = Mathf.Pi;
        _pitch = 0.35f;

        if (_source is null)
        {
            return;
        }

        foreach (var marker in _source.SurfaceSkyMarkers())
        {
            if (marker.BodyId != "sun")
            {
                continue;
            }

            var dir = marker.ScenePosition.Normalized();
            _yaw = Mathf.Atan2(dir.X, dir.Z);
            _pitch = Mathf.Asin(Mathf.Clamp(dir.Y, -1.0f, 1.0f));
            return;
        }
    }

    public override void _Process(double delta)
    {
        if (!_active || _source is null)
        {
            return;
        }

        RefreshMarkers();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_active)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } button:
                _looking = button.Pressed;
                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseMotion motion when _looking:
                _yaw -= motion.Relative.X * LookSensitivity;
                _pitch = Mathf.Clamp(
                    _pitch - motion.Relative.Y * LookSensitivity,
                    -1.2f,
                    1.2f);
                ApplyLook();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void ApplyLook()
    {
        var eye = new Vector3(0.0f, 1.6f, 0.0f);
        var forward = new Vector3(
            Mathf.Cos(_pitch) * Mathf.Sin(_yaw),
            Mathf.Sin(_pitch),
            Mathf.Cos(_pitch) * Mathf.Cos(_yaw));
        _camera.LookAtFromPosition(eye, eye + forward, Vector3.Up);
    }

    private void RefreshMarkers()
    {
        if (_source is null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marker in _source.SurfaceSkyMarkers())
        {
            seen.Add(marker.BodyId);
            if (!_meshes.TryGetValue(marker.BodyId, out var mesh))
            {
                mesh = CreateMesh(marker);
                _markers.AddChild(mesh);
                _meshes[marker.BodyId] = mesh;

                var label = new Label3D
                {
                    Text = marker.Name,
                    FontSize = 42,
                    Modulate = Colors.White,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                };
                _markers.AddChild(label);
                _labels[marker.BodyId] = label;
            }

            mesh.Position = marker.ScenePosition;
            if (_labels.TryGetValue(marker.BodyId, out var caption))
            {
                caption.Position = marker.ScenePosition * 1.02f;
            }
        }

        foreach (var id in _meshes.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            _meshes[id].QueueFree();
            _meshes.Remove(id);
            if (_labels.Remove(id, out var label))
            {
                label.QueueFree();
            }
        }
    }

    private static MeshInstance3D CreateMesh(SurfaceSkyMarker marker)
    {
        var radius = marker.BodyId == "sun" ? 12.0f : marker.BodyId == "moon" ? 6.0f : 3.5f;
        return new MeshInstance3D
        {
            Name = marker.BodyId,
            Mesh = new SphereMesh
            {
                Radius = radius,
                Height = radius * 2.0f,
                RadialSegments = 16,
                Rings = 8,
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = marker.Color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = marker.Color,
                EmissionEnergyMultiplier = marker.BodyId is "sun" or "moon" ? 1.2f : 0.6f,
            },
        };
    }

    private void ClearMarkers()
    {
        foreach (var mesh in _meshes.Values)
        {
            mesh.QueueFree();
        }

        foreach (var label in _labels.Values)
        {
            label.QueueFree();
        }

        _meshes.Clear();
        _labels.Clear();
    }
}
