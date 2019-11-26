using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.Tilemaps
{
	/// <summary>
	/// This Brush instances and places a randomly selected Prefabs onto the targeted location and parents the instanced object to the paint target. Use this as an example to quickly place an assorted type of GameObjects onto structured locations.
	/// </summary>
	[CreateAssetMenu(fileName = "Prefab brush", menuName = "Brushes/Prefab brush")]
	[CustomGridBrush(false, true, false, "Prefab Brush")]
	public class PrefabBrush : GridBrush
	{
		private const float k_PerlinOffset = 100000f;
		/// <summary>
		/// The selection of Prefabs to paint from
		/// </summary>
		public GameObject[] m_Prefabs;
		/// <summary>
		/// Factor for distribution of choice of Prefabs to paint
		/// </summary>
		public float m_PerlinScale = 0.5f;
		/// <summary>
		/// Anchor Point of the Instantiated Prefab in the cell when painting
		/// </summary>
		public Vector3 m_Anchor = new Vector3(0.5f, 0.5f, 0.5f);

		private GameObject prev_brushTarget;
		private Vector3Int prev_position = Vector3Int.one * int.MaxValue;

		private int m_rotationStep, m_depthStep;

		/// <summary>
		/// Paints Prefabs into a given position within the selected layers.
		/// The PrefabBrush overrides this to provide Prefab painting functionality.
		/// </summary>
		/// <param name="gridLayout">Grid used for layout.</param>
		/// <param name="brushTarget">Target of the paint operation. By default the currently selected GameObject.</param>
		/// <param name="position">The coordinates of the cell to paint data to.</param>
		public override void Paint(GridLayout grid, GameObject brushTarget, Vector3Int position)
		{
			if (position == prev_position)
			{
				return;
			}
			prev_position = position;
			if (brushTarget) {
				prev_brushTarget = brushTarget;
			}
			brushTarget = prev_brushTarget;

			// Do not allow editing palettes
			if (brushTarget.layer == 31)
				return;

			int index = Mathf.Clamp(Mathf.FloorToInt(GetPerlinValue(position, m_PerlinScale, k_PerlinOffset) * m_Prefabs.Length), 0, m_Prefabs.Length - 1);
			GameObject prefab = m_Prefabs[index];
			GameObject instance = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
			if (instance != null)
			{
				Erase(grid, brushTarget, position);

				Undo.MoveGameObjectToScene(instance, brushTarget.scene, "Paint Prefabs");
				Undo.RegisterCreatedObjectUndo(instance, "Paint Prefabs");
				instance.transform.SetParent(brushTarget.transform);

				GetPositionAndRotation(grid, position, out Vector3 worldPos, out Quaternion worldRot);
				instance.transform.SetPositionAndRotation(worldPos, worldRot);
			}
		}

		/// <summary>
		/// Erases Prefabs in a given position within the selected layers.
		/// The PrefabBrush overrides this to provide Prefab erasing functionality.
		/// </summary>
		/// <param name="gridLayout">Grid used for layout.</param>
		/// <param name="brushTarget">Target of the erase operation. By default the currently selected GameObject.</param>
		/// <param name="position">The coordinates of the cell to erase data from.</param>
		public override void Erase(GridLayout grid, GameObject brushTarget, Vector3Int position)
		{
			if (brushTarget)
			{
				prev_brushTarget = brushTarget;
			}
			brushTarget = prev_brushTarget;
			// Do not allow editing palettes
			if (brushTarget.layer == 31)
				return;

			Transform erased = GetObjectInCell(grid, brushTarget.transform, position);
			if (erased != null)
				Undo.DestroyObjectImmediate(erased.gameObject);
		}

		private static Transform GetObjectInCell(GridLayout grid, Transform parent, Vector3Int position)
		{
			int childCount = parent.childCount;
			Vector3 min = grid.LocalToWorld(grid.CellToLocalInterpolated(position));
			Vector3 max = grid.LocalToWorld(grid.CellToLocalInterpolated(position + Vector3Int.one));
			Bounds bounds = new Bounds((max + min)*.5f, max - min);

			for (int i = 0; i < childCount; i++)
			{
				Transform child = parent.GetChild(i);
				if (bounds.Contains(child.position))
					return child;
			}
			return null;
		}

		private static float GetPerlinValue(Vector3Int position, float scale, float offset)
		{
			return Mathf.PerlinNoise((position.x + offset)*scale, (position.y + offset)*scale);
		}

		public override void Rotate(RotationDirection direction, GridLayout.CellLayout layout)
		{
			switch (direction)
			{
				case RotationDirection.Clockwise:
					m_rotationStep = Mod(m_rotationStep + 1, 4);
					break;

				case RotationDirection.CounterClockwise:
					m_rotationStep = Mod(m_rotationStep - 1, 4);
					break;
			}

			base.Rotate(direction, layout);
		}

		public override void ChangeZPosition(int change)
		{
			m_depthStep += change;
			base.ChangeZPosition(change);
		}

		private int Mod(int a, int b)
		{
			return (a % b + b) % b;
		}

		public void GetPositionAndRotation(GridLayout grid, Vector3Int coordinate, out Vector3 position, out Quaternion rotation)
		{
			position = grid.LocalToWorld(grid.CellToLocalInterpolated(coordinate + m_Anchor))
				+ (Vector3.up * grid.cellSize.z * m_depthStep);
			rotation = Quaternion.Euler(0f, 90f * m_rotationStep, 0f);
		}

		public void ResetSteps()
		{
			m_rotationStep = m_depthStep = 0;
		}
	}

	/// <summary>
	/// The Brush Editor for a Prefab Brush.
	/// </summary>
	[CustomEditor(typeof(PrefabBrush))]
	public class PrefabBrushEditor : GridBrushEditor
	{
		private PrefabBrush prefabBrush => target as PrefabBrush;

		private SerializedProperty m_Prefabs;
		private SerializedProperty m_Anchor;
		private SerializedObject m_SerializedObject;

		private GameObject m_previewBrush;

		protected GameObject previewBrush
		{
			get
			{
				if (m_previewBrush == null)
				{
					if (prefabBrush.m_Prefabs != null && prefabBrush.m_Prefabs.Length > 0)
					{
						var template = prefabBrush.m_Prefabs[0];
						if (template != null)
						{
							m_previewBrush = Instantiate(template);
							m_previewBrush.hideFlags |= HideFlags.HideAndDontSave;
						}
					}
				}
				return m_previewBrush;
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			m_SerializedObject = new SerializedObject(target);
			m_Prefabs = m_SerializedObject.FindProperty("m_Prefabs");
			m_Anchor = m_SerializedObject.FindProperty("m_Anchor");

			prefabBrush.ResetSteps();
			prefabBrush.canChangeZPosition = true;
		}

		protected override void OnDisable()
		{
			base.OnDisable();

			if (previewBrush != null)
			{
				DestroyImmediate(previewBrush);
			}
		}

		/// <summary>
		/// Callback for painting the inspector GUI for the PrefabBrush in the Tile Palette.
		/// The PrefabBrush Editor overrides this to have a custom inspector for this Brush.
		/// </summary>
		public override void OnPaintInspectorGUI()
		{
			m_SerializedObject.UpdateIfRequiredOrScript();
			prefabBrush.m_PerlinScale = EditorGUILayout.Slider("Perlin Scale", prefabBrush.m_PerlinScale, 0.001f, 0.999f);
			EditorGUILayout.PropertyField(m_Prefabs, true);
			EditorGUILayout.PropertyField(m_Anchor);
			m_SerializedObject.ApplyModifiedPropertiesWithoutUndo();
		}

		public override void OnPaintSceneGUI(GridLayout grid, GameObject brushTarget, BoundsInt position, GridBrushBase.Tool tool, bool executing)
		{
			base.OnPaintSceneGUI(grid, brushTarget, position, tool, executing);

			if (previewBrush != null)
			{
				prefabBrush.GetPositionAndRotation(grid, position.position, out Vector3 worldPos, out Quaternion worldRot);
				previewBrush.transform.SetPositionAndRotation(worldPos, worldRot);
			}

			var labelText = "Pos: " + position.position;
			if (position.size.x > 1 || position.size.y > 1)
			{
				labelText += " Size: " + position.size;
			}

			Handles.Label(grid.CellToWorld(position.position), labelText);
		}
	}
}