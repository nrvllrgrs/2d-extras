using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

namespace UnityEditor.Tilemaps
{
	/// <summary>
	/// This Brush instances and places a randomly selected Prefabs onto the targeted location and parents the instanced object to the paint target. Use this as an example to quickly place an assorted type of GameObjects onto structured locations.
	/// </summary>
	[CreateAssetMenu(fileName = "Prefab brush", menuName = "Brushes/Prefab brush")]
	[CustomGridBrush(false, true, false, "Prefab Brush")]
	public class PrefabBrush : GridBrush
	{
		[HideIf("m_Weighted")]
		/// <summary>
		/// The selection of Prefabs to paint from
		/// </summary>
		public GameObject[] m_Prefabs;

		[ShowIf("m_Weighted")]
		public WeightedPrefab[] m_WeightedPrefabs;

		public bool m_Weighted;
		public bool m_RandomRotation;

		/// <summary>
		/// Anchor Point of the Instantiated Prefab in the cell when painting
		/// </summary>
		public Vector3 m_Anchor = new Vector3(0.5f, 0.5f, 0f);

		private GameObject prev_brushTarget;
		private Vector3Int prev_position = Vector3Int.one * int.MaxValue;

		private int m_rotationStep;

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

			GameObject prefab;
			if (!m_Weighted)
			{
				// No valid templates, skip
				var validTemplates = m_Prefabs.Where(x => x != null);
				if (!validTemplates.Any())
					return;

				prefab = m_Prefabs[Random.Range(0, validTemplates.Count())];
			}
			else
			{
				var validTemplates = m_WeightedPrefabs.Where(x => x.on && x.item != null);
				if (!validTemplates.Any())
					return;

				prefab = GetWeightedRandom(validTemplates);
			}

			GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
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
			if (m_RandomRotation)
				return;

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

		private int Mod(int a, int b)
		{
			return (a % b + b) % b;
		}

		public void GetPositionAndRotation(GridLayout grid, Vector3Int coordinate, out Vector3 position, out Quaternion rotation, bool preview = false)
		{
			position = grid.LocalToWorld(grid.CellToLocalInterpolated(coordinate + m_Anchor));

			int rotationStep = m_RandomRotation && !preview
				? Random.Range(0, 4)
				: m_rotationStep;
			rotation = Quaternion.Euler(0f, 90f * rotationStep, 0f);
		}

		public void ResetSteps()
		{
			m_rotationStep = 0;
		}

		private int GetWeightedRandomIndex(IEnumerable<float> items, System.Random random = null)
		{
			float totalWeights = items.Sum();
			float value = random != null
				? (float)random.NextDouble() * totalWeights
				: Random.Range(0f, totalWeights);

			int count = items.Count();
			for (int i = 0; i < count - 1; ++i)
			{
				var weight = items.ElementAt(i);
				if (value < weight)
				{
					return i;
				}

				value -= weight;
			}

			return items.Count() - 1;
		}

		private T GetWeightedRandom<T>(IEnumerable<IWeightedItem<T>> items, System.Random random = null)
		{
			return items.ElementAt(GetWeightedRandomIndex(items.Select(x => x.weight), random)).item;
		}

		#region Structures

		[System.Serializable]
		public class WeightedPrefab : IWeightedItem<GameObject>
		{
			[SerializeField, InlineEditor(InlineEditorModes.SmallPreview)]
			private GameObject m_prefab;

			[SerializeField]
			private float m_weight = 1f;

			public bool on = true;

			public GameObject item => m_prefab;
			public float weight => m_weight;
		}

		public interface IWeightedItem<T>
		{
			T item { get; }
			float weight { get; }
		}

		#endregion
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
					if (!prefabBrush.m_Weighted)
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
					else
					{
						var info = prefabBrush.m_WeightedPrefabs.FirstOrDefault(x => x.on);
						if (info != null)
						{
							m_previewBrush = Instantiate(info.item);
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

		public override void OnToolDeactivated(GridBrushBase.Tool tool)
		{
			base.OnToolDeactivated(tool);

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
			EditorGUILayout.PropertyField(m_Prefabs, true);
			EditorGUILayout.PropertyField(m_Anchor);
			m_SerializedObject.ApplyModifiedPropertiesWithoutUndo();
		}

		public override void OnPaintSceneGUI(GridLayout grid, GameObject brushTarget, BoundsInt position, GridBrushBase.Tool tool, bool executing)
		{
			base.OnPaintSceneGUI(grid, brushTarget, position, tool, executing);

			if (previewBrush != null)
			{
				prefabBrush.GetPositionAndRotation(grid, position.position, out Vector3 worldPos, out Quaternion worldRot, true);
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