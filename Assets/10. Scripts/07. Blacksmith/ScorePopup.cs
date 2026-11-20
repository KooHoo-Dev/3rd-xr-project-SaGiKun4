using TMPro;
using UnityEngine;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// "+1" 같은 월드 텍스트를 띄워 위로 올라가며 사라지게 한다. 항상 카메라를 바라본다.
    /// </summary>
    public class ScorePopup : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.9f;
        [SerializeField] private float riseSpeed = 0.4f;

        private TextMeshPro text;
        private Color baseColor;
        private float age;

        public static ScorePopup Spawn(string message, Vector3 position, Color color)
        {
            var go = new GameObject("ScorePopup");
            go.transform.position = position;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = message;
            tmp.fontSize = 2.5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(1f, 0.4f);

            var popup = go.AddComponent<ScorePopup>();
            popup.text = tmp;
            popup.baseColor = color;
            return popup;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            if (text != null)
            {
                var c = baseColor;
                c.a = 1f - Mathf.Clamp01(age / lifetime);
                text.color = c;
            }

            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
