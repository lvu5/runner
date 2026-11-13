using UnityEngine;

namespace InfiniteRunner
{
    public sealed class RunnerHud : MonoBehaviour
    {
        private GameManager gameManager;
        private TrackManager trackManager;
        private GUIStyle statStyle;
        private GUIStyle smallStyle;
        private GUIStyle titleStyle;
        private GUIStyle centeredStyle;
        private GUIStyle panelStyle;
        private Texture2D panelTexture;

        public void Configure(GameManager manager, TrackManager track)
        {
            gameManager = manager;
            trackManager = track;
        }

        private void OnGUI()
        {
            if (gameManager == null)
            {
                return;
            }

            EnsureStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.75f, 1.5f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            DrawTopBar(width);
            DrawPoseStatus(width);

            if (gameManager.IsRunning)
            {
                DrawControls(width, height);
            }
            else
            {
                DrawGameOver(width, height);
            }

            GUI.matrix = oldMatrix;
        }

        private void DrawPoseStatus(float width)
        {
            MediaPipeBridge pose = MediaPipeBridge.Instance;
            if (pose == null)
            {
                return;
            }

            float panelWidth = Mathf.Min(560f, width - 44f);
            GUI.Box(new Rect(22f, 110f, panelWidth, 42f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(34f, 116f, panelWidth - 24f, 30f), pose.StatusText, centeredStyle);
        }

        private void DrawTopBar(float width)
        {
            float statsWidth = Mathf.Min(390f, width - 44f);
            GUI.Box(new Rect(22f, 20f, statsWidth, 80f), GUIContent.none, panelStyle);
            string location = trackManager != null ? trackManager.RouteLocation : "SAI GON / DISTRICT 1";
            GUI.Label(new Rect(38f, 29f, statsWidth - 32f, 24f), location, smallStyle);
            GUI.Label(new Rect(38f, 58f, 172f, 30f), $"{gameManager.DistanceMeters:0000} m", statStyle);
            GUI.Label(new Rect(202f, 58f, 175f, 30f), $"COINS  {gameManager.Coins:000}", statStyle);

            if (width > 630f)
            {
                GUI.Box(new Rect(width - 190f, 20f, 168f, 80f), GUIContent.none, panelStyle);
                GUI.Label(new Rect(width - 176f, 30f, 148f, 28f), $"BEST  {Mathf.Max(gameManager.BestScore, gameManager.Score):0000}", smallStyle);
                GUI.Label(new Rect(width - 176f, 62f, 148f, 24f), $"SPEED  {gameManager.CurrentSpeed:0.0}", smallStyle);
            }
        }

        private void DrawControls(float width, float height)
        {
            if (gameManager.RunTime < 8f)
            {
                MediaPipeBridge bridge = MediaPipeBridge.Instance;
                string controls = bridge != null && bridge.AcceptsPoseInput
                    ? "CAMERA  Cross LEFT / CENTER / RIGHT sections to change lanes  •  Run  •  Jump  •  Crouch"
                    : "KEYBOARD  Hold W run  •  A / D move  •  SPACE jump  •  Hold S crouch";

                float panelWidth = Mathf.Min(660f, width - 44f);
                GUI.Box(new Rect((width - panelWidth) * 0.5f, height - 108f, panelWidth, 76f), GUIContent.none, panelStyle);
                GUI.Label(
                    new Rect((width - panelWidth) * 0.5f + 16f, height - 104f, panelWidth - 32f, 68f),
                    controls,
                    centeredStyle);
            }
        }

        private void DrawGameOver(float width, float height)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.77f, 0.87f, 0.91f, 0.48f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float panelWidth = Mathf.Min(470f, width - 32f);
            float panelHeight = 270f;
            Rect panel = new Rect(
                width * 0.5f - panelWidth * 0.5f,
                height * 0.5f - panelHeight * 0.5f,
                panelWidth,
                panelHeight);

            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 35f, panelWidth - 40f, 60f), "RUN OVER", titleStyle);
            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 112f, panelWidth - 40f, 40f),
                $"SCORE  {gameManager.Score:0000}     BEST  {gameManager.BestScore:0000}",
                centeredStyle);
            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 184f, panelWidth - 40f, 38f),
                "Press R or ENTER to run again",
                centeredStyle);
        }

        private void EnsureStyles()
        {
            if (statStyle != null)
            {
                return;
            }

            Color lightText = new Color(0.09f, 0.23f, 0.3f);
            statStyle = new GUIStyle(GUI.skin.label);
            statStyle.fontSize = 21;
            statStyle.fontStyle = FontStyle.Bold;
            statStyle.normal.textColor = lightText;

            smallStyle = new GUIStyle(statStyle);
            smallStyle.fontSize = 17;
            smallStyle.normal.textColor = new Color(0.03f, 0.4f, 0.53f);

            titleStyle = new GUIStyle(statStyle);
            titleStyle.fontSize = 42;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = new Color(0.83f, 0.29f, 0.17f);

            centeredStyle = new GUIStyle(statStyle);
            centeredStyle.fontSize = 17;
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.wordWrap = true;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelTexture = MakeTexture(new Color(1f, 0.99f, 0.94f, 0.92f));
            panelStyle.normal.background = panelTexture;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.name = "HUD Background";
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (panelTexture != null)
            {
                Destroy(panelTexture);
            }
        }
    }
}
