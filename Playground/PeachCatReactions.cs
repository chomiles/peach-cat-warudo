using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Plugins;
using Warudo.Core.Graphs;
using Warudo.Plugins.Core.Assets.Prop;

namespace PeachCat.Warudo
{
    [PluginType(Id = "peachcat.warudo.reactions", Name = "Peach Cat Reactions", Author = "chomiles", Version = "1.5.0", NodeTypes = new[] { typeof(CatChatReadingNode) })]
    public sealed class CatReactions : Plugin
    {
        const string ReceiverNode = "Warudo.Plugins.iFacialMocap.Nodes.GetIFacialMocapReceiverDataNode";
        const string CatFile = "/PeachCat.warudo";
        Node receiver;
        float nextReceiverCheck;
        bool trackingWasActive;
        [DataInput, Label("Body Color")]
        public Color BodyColor = new Color32(255, 205, 152, 255);
        [DataInput, Label("Blush Color")]
        public Color BlushColor = new Color32(246, 183, 133, 255);
        [DataInput, Label("Eye Color")]
        public Color EyeColor = new Color32(8, 12, 19, 255);

        [Section("Face Tracking"), DataInput, Label("Gaze Sensitivity"), FloatSlider(.5f, 2f, .05f)]
        public float GazeSensitivity = 1.2f;
        [DataInput, Label("Gaze Delay (Seconds)"), FloatSlider(0f, 2f, .05f)]
        public float GazeDelay = .8f;
        [DataInput, Label("Smile Delay (Seconds)"), FloatSlider(0f, 2f, .05f)]
        public float SmileDelay = .8f;
        [DataInput, Label("Angry Threshold"), FloatSlider(.27f, 1f, .01f)]
        public float AngryThreshold = .65f;
        [DataInput, Label("Smile Bounce Strength"), FloatSlider(0f, 2f, .05f)]
        public float SmileBounceStrength = 1f;

        [Section("Idle Motion"), DataInput, Label("Random Idle Gestures")]
        public bool RandomIdleGestures = true;
        [DataInput, Label("Idle Gesture Interval Multiplier"), FloatSlider(.5f, 3f, .1f)]
        public float IdleInterval = 1f;

        [Section("Chat Reading"), DataInput, Label("React to Chat Events")]
        public bool ReactToChat = true;
        [DataInput, Label("Use Local Overlay Feed")]
        public bool UseOverlayFeed = true;
        [DataInput, Label("Reading Duration (Seconds)"), FloatSlider(1f, 6f, .1f)]
        public float ReadingDuration = 2.6f;
        [DataInput, Label("Reading Side Angle"), FloatSlider(5f, 35f, 1f)]
        public float ReadingSideAngle = 20f;
        [DataInput, Label("Reading Down Angle"), FloatSlider(0f, 35f, 1f)]
        public float ReadingDownAngle = 21f;

        float ReadDuration => Mathf.Clamp(ReadingDuration, 1f, 6f);
        bool overlayRequested;
        bool previewIdle;
        static readonly HttpClient ChatClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        CancellationTokenSource chatLifetime;
        int pendingChat;
        float readStarted = -100f, readCooldown;
        float nextGesture, gestureStart, gestureSize, gestureRoll;
        enum IdleGesture { Size, Spin, RoundEyes, VerticalSpin }
        IdleGesture idleGesture = IdleGesture.Size;
        float spinDirection, roundEyes;
        Quaternion baseRotation, spinOffset;
        Vector3 originalScale;

        [Trigger, Label("Preview Spin")]
        public void PreviewSpin() => PreviewGesture(IdleGesture.Spin);

        [Trigger, Label("Preview Vertical Spin")]
        public void PreviewVerticalSpin() => PreviewGesture(IdleGesture.VerticalSpin);

        [Trigger, Label("Preview Round Eyes")]
        public void PreviewRoundEyes() => PreviewGesture(IdleGesture.RoundEyes);
        float sizeFactor = 1f;

        [Trigger, Label("Preview Chat Reading")]
        public void PreviewChatReading()
        {
            if (body == null) return;
            readStarted = Time.time;
            readCooldown = readStarted + ReadDuration + .7f;
        }

        const float EyeSize = 1.5f;
        enum Mood { Neutral, Smile, Sad, Angry }

        GameObject catRoot;
        Animator catAnimator;
        Transform body, leftEye, rightEye;
        Renderer bodyRenderer, leftRenderer, rightRenderer;
        MeshRenderer leftArc, rightArc, leftCheek, rightCheek;
        Renderer leftRound, rightRound;
        Material bodyMaterial, eyeMaterial, cheekMaterial;
        Material originalBody, originalLeftEye, originalRightEye;
        bool animatorWasEnabled, following;
        Mood mood;
        float nextBlink, blinkStart, nextLook, bounce, bouncePhase, smileStart, gazeStart;
        Vector3 idleAngles, gazeCenter;

        public override void OnUpdate()
        {
            base.OnUpdate();
            var scene = Context.OpenedScene;
            var prop = scene?.GetAssets<PropAsset>().FirstOrDefault(p =>
                p.Active && p.GameObject != null && p.Source != null &&
                p.Source.ToString().EndsWith(CatFile, StringComparison.OrdinalIgnoreCase));
            var nextRoot = prop?.GameObject;
            if (nextRoot != catRoot)
            {
                Unbind();
                if (nextRoot == null || !Bind(nextRoot)) return;
            }
            if (body == null) return;
            UpdateColors();
            UpdateChatSource();

            float now = Time.time;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (now >= nextReceiverCheck)
            {
                nextReceiverCheck = now + 1f;
                receiver = scene.GetGraphs().Values.Where(g => g.Enabled).SelectMany(g => g.GetNodes().Values)
                    .FirstOrDefault(n => n.GetType().FullName == ReceiverNode &&
                        n.GetDataOutputPort("IsTracked")?.ComputedValue() is true);
            }
            bool tracking = receiver?.GetDataOutputPort("IsTracked")?.ComputedValue() is true;
            var shapes = tracking
                ? receiver.GetDataOutputPort("BlendShapes")?.ComputedValue() as IReadOnlyDictionary<string, float>
                : null;
            var rotations = tracking
                ? receiver.GetDataOutputPort("BoneRotations")?.ComputedValue() as Quaternion[]
                : null;
            bool active = shapes != null && shapes.Count > 0;
            if (active && !trackingWasActive)
                Debug.Log("[Peach Cat] iFacialMocap ARKit input: " + shapes.Count + " channels, " +
                    (rotations == null ? 0 : rotations.Length) + " rotations; smile=" +
                    Value(shapes, "mouthSmileLeft").ToString("F2") + ", browDown=" +
                    Value(shapes, "browDownLeft").ToString("F2"));
            trackingWasActive = active;

            float smile = Pair(shapes, "mouthSmileLeft", "mouthSmileRight");
            float frown = Pair(shapes, "mouthFrownLeft", "mouthFrownRight");
            float sad = Mathf.Max(frown, frown * .7f + Value(shapes, "browInnerUp") * .3f);
            float loweredBrows = Pair(shapes, "browDownLeft", "browDownRight");
            float angry = loweredBrows * .8f + Mathf.Max(
                Pair(shapes, "eyeSquintLeft", "eyeSquintRight"), Pair(shapes, "noseSneerLeft", "noseSneerRight")) * .2f;
            Mood nextMood = Mood.Neutral;
            float best = .27f;
            if (smile >= best) { best = smile; nextMood = Mood.Smile; }
            if (sad >= best) { best = sad; nextMood = Mood.Sad; }
            float threshold = Mathf.Clamp(AngryThreshold, .27f, 1f);
            float angryThreshold = mood == Mood.Angry ? threshold * (.32f / .65f) : threshold;
            if (angry >= Mathf.Max(best, angryThreshold)) nextMood = Mood.Angry;
            if (nextMood == Mood.Neutral && mood != Mood.Angry && CurrentWeight(smile, sad) > .17f) nextMood = mood;
            bool expressionDetected = nextMood != Mood.Neutral;
            if (nextMood == Mood.Smile)
            {
                if (smileStart < 0f) smileStart = now;
                if (now - smileStart < Mathf.Clamp(SmileDelay, 0f, 2f)) nextMood = Mood.Neutral;
                else if (mood != Mood.Smile) bouncePhase = 0f;
            }
            else
            {
                smileStart = -1f;
            }
            mood = nextMood;

            int chatEvent = Interlocked.Exchange(ref pendingChat, 0);
            if (ReactToChat && chatEvent != 0 && now >= readCooldown)
            {
                PreviewChatReading();
                Debug.Log(chatEvent == 2
                    ? "[Peach Cat] Reading motion triggered by Blueprint chat input."
                    : "[Peach Cat] Reading motion triggered by CHZZK chat.");
            }
            Vector3 target = Gaze(now, expressionDetected, shapes, rotations);
            float readTime = now - readStarted;
            float reading = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(readTime / .23f)) *
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ReadDuration - readTime) / .36f));
            if (reading > 0f)
            {
                float row = Mathf.Repeat(readTime / .69f, 1f);
                float angle = Mathf.Clamp(ReadingSideAngle, 5f, 35f);
                float scan = row < .79f
                    ? Mathf.Lerp(-angle, angle, row / .79f)
                    : Mathf.Lerp(angle, -angle, Mathf.SmoothStep(0f, 1f, (row - .79f) / .21f));
                var readingPose = new Vector3(Mathf.Clamp(ReadingDownAngle, 0f, 35f) + Mathf.Min(7.5f, Mathf.Floor(readTime / .69f) * 2.5f), scan, -4f + scan * .2f);
                target = Vector3.Lerp(target, readingPose, reading);
            }
            if (mood == Mood.Sad) target.x += 10f;
            if (mood == Mood.Angry) target.x += 5f;
            bounce = Mathf.MoveTowards(bounce, mood == Mood.Smile ? Mathf.Max(.35f, smile) : 0f, dt * 4f);
            if (bounce > 0f) bouncePhase += dt * Mathf.PI * 2f * 1.55f;
            float side = Mathf.Sin(bouncePhase) * Mathf.Clamp(SmileBounceStrength, 0f, 2f);
            body.localPosition = new Vector3(side * .020f * bounce, Mathf.Abs(side) * .013f * bounce, 0);
            target.z -= side * 7f * bounce;
            bool idleAllowed = !expressionDetected && !following && readTime >= ReadDuration;
            UpdateIdleGesture(now, dt, reading, idleAllowed);
            baseRotation = Quaternion.Slerp(baseRotation, Quaternion.Euler(target), 1f - Mathf.Exp(-dt * 10f));
            body.localRotation = baseRotation * spinOffset;
            UpdateEyes(now, dt, reading);
        }

        bool Bind(GameObject root)
        {
            var candidate = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Bounce");
            var left = candidate?.Find("Gaze/Eyes/Eye L");
            var right = candidate?.Find("Gaze/Eyes/Eye R");
            var shell = candidate?.Find("Face and ears")?.GetComponent<Renderer>();
            if (left == null || right == null || shell == null) return false;
            catRoot = root;
            body = candidate;
            originalScale = body.localScale;
            baseRotation = body.localRotation;
            spinOffset = Quaternion.identity;
            roundEyes = 0f;
            sizeFactor = 1f;
            nextGesture = Time.time + 2f;
            gestureStart = readStarted = -100f;
            readCooldown = 0f;
            previewIdle = false;
            leftEye = left;
            rightEye = right;
            nextReceiverCheck = 0f;
            catAnimator = body.GetComponentInParent<Animator>();
            if (catAnimator != null)
            {
                animatorWasEnabled = catAnimator.enabled;
                catAnimator.enabled = false;
            }
            leftEye.localPosition = new Vector3(-.026f, 0, 0);
            rightEye.localPosition = new Vector3(.026f, 0, 0);
            leftRenderer = leftEye.GetComponent<Renderer>();
            rightRenderer = rightEye.GetComponent<Renderer>();
            bodyRenderer = shell;
            originalBody = bodyRenderer.sharedMaterial;
            originalLeftEye = leftRenderer.sharedMaterial;
            originalRightEye = rightRenderer.sharedMaterial;
            bodyMaterial = new Material(originalBody);
            eyeMaterial = new Material(originalLeftEye);
            cheekMaterial = new Material(originalLeftEye);
            bodyRenderer.sharedMaterial = bodyMaterial;
            leftRenderer.sharedMaterial = rightRenderer.sharedMaterial = eyeMaterial;
            leftArc = MakeArc(leftEye, eyeMaterial);
            rightArc = MakeArc(rightEye, eyeMaterial);
            leftRound = MakeRoundEye(leftEye, eyeMaterial);
            rightRound = MakeRoundEye(rightEye, eyeMaterial);
            leftCheek = MakeCheek(leftEye.parent, -.055f, cheekMaterial);
            rightCheek = MakeCheek(rightEye.parent, .055f, cheekMaterial);
            UpdateColors();
            nextBlink = Time.time + 1.8f;
            nextLook = Time.time + 2.6f;
            blinkStart = -100f;
            gazeCenter = Vector3.zero;
            idleAngles = Vector3.zero;
            following = false;
            gazeStart = -1f;
            mood = Mood.Neutral;
            smileStart = -1f;
            bounce = 0f;
            Debug.Log("[Peach Cat] ARKit reactions attached to the peach cat");
            return true;
        }

        void PreviewGesture(IdleGesture gesture)
        {
            BeginIdleGesture(gesture, Time.time);
            previewIdle = body != null;
        }

        public void QueueBlueprintChat()
        {
            if (ReactToChat) Interlocked.Exchange(ref pendingChat, 2);
        }

        void BeginIdleGesture(IdleGesture gesture, float now)
        {
            if (body == null) return;
            idleGesture = gesture;
            gestureStart = now;
            gestureSize = UnityEngine.Random.value < .55f ? -.18f : .12f;
            gestureRoll = UnityEngine.Random.Range(-.035f, .035f);
            spinDirection = UnityEngine.Random.value < .5f ? -1f : 1f;
            nextGesture = now + UnityEngine.Random.Range(3.2f, 6.8f) * Mathf.Clamp(IdleInterval, .5f, 3f);
        }

        void UpdateIdleGesture(float now, float dt, float reading, bool idleAllowed)
        {
            if (previewIdle && (now - gestureStart >= 1.15f || now - readStarted < ReadDuration))
                previewIdle = false;
            if (!previewIdle && (!idleAllowed || !RandomIdleGestures)) gestureStart = -100f;
            else if (!previewIdle && now >= nextGesture)
            {
                int offset = UnityEngine.Random.Range(1, 4);
                BeginIdleGesture((IdleGesture)(((int)idleGesture + offset) % 4), now);
            }

            float elapsed = now - gestureStart;
            float phase = Mathf.Clamp01(elapsed / 1.15f);
            float envelope = Mathf.Sin(phase * Mathf.PI);
            float pulse = idleGesture == IdleGesture.Size ? envelope * gestureSize : 0f;
            bool vertical = idleGesture == IdleGesture.VerticalSpin;
            if ((vertical || idleGesture == IdleGesture.Spin) && elapsed < 1.05f)
            {
                float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.05f));
                float angle = 360f * turn * spinDirection;
                spinOffset = vertical
                    ? Quaternion.Euler(angle, 0f, 0f)
                    : Quaternion.Euler(0f, angle, 6f * envelope * spinDirection);
                pulse = -.08f * envelope;
            }
            else
            {
                spinOffset = Quaternion.Slerp(spinOffset, Quaternion.identity, 1f - Mathf.Exp(-dt * 18f));
            }
            float roundTarget = idleGesture == IdleGesture.RoundEyes && elapsed < .95f ? 1f : 0f;
            roundEyes = Mathf.MoveTowards(roundEyes, roundTarget, dt * 8f);
            float desired = 1f + Mathf.Lerp(pulse, .07f, reading);
            sizeFactor = Mathf.Lerp(sizeFactor, desired, 1f - Mathf.Exp(-dt * 12f));
            float stretch = idleGesture == IdleGesture.Size ? 1f + envelope * gestureRoll * (1f - reading) : 1f;
            body.localScale = Vector3.Scale(originalScale,
                new Vector3(sizeFactor * stretch, sizeFactor / stretch, sizeFactor));
        }

        static Renderer MakeRoundEye(Transform eye, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Round idle eye";
            go.transform.SetParent(eye.parent, false);
            go.transform.localPosition = eye.localPosition;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = false;
            return renderer;
        }

        void UpdateChatSource()
        {
            bool wanted = ReactToChat && UseOverlayFeed;
            if (wanted == overlayRequested) return;
            overlayRequested = wanted;
            if (wanted) StartChat();
            else StopChat();
        }

        void StopChat()
        {
            if (chatLifetime == null) return;
            chatLifetime.Cancel();
            chatLifetime.Dispose();
            chatLifetime = null;
        }

        void StartChat()
        {
            try
            {
                string path = Context.PersistentDataManager.GetFullPath("PeachCatChatUrl.txt");
                if (!File.Exists(path)) return;
                string address = File.ReadAllText(path).Trim();
                if (!Uri.TryCreate(address, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
                    !url.AbsolutePath.EndsWith("/events", StringComparison.Ordinal) || !string.IsNullOrEmpty(url.UserInfo))
                {
                    Debug.LogWarning("[Peach Cat] Invalid chat event URL.");
                    return;
                }
                chatLifetime = new CancellationTokenSource();
                _ = ReadChatEvents(url, chatLifetime.Token);
            }
            catch (IOException)
            {
                Debug.LogWarning("[Peach Cat] Could not read the local chat configuration.");
            }
        }

        async Task ReadChatEvents(Uri url, CancellationToken lifetime)
        {
            while (!lifetime.IsCancellationRequested)
            {
                try
                {
                    using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(lifetime))
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        attempt.CancelAfter(TimeSpan.FromSeconds(40));
                        request.Headers.Accept.ParseAdd("text/event-stream");
                        using (var response = await ChatClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attempt.Token).ConfigureAwait(false))
                        {
                            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/event-stream")
                            {
                                Debug.LogWarning("[Peach Cat] Chat stream unavailable, status " + (int)response.StatusCode);
                                return;
                            }
                            Debug.Log("[Peach Cat] Connected to the configured chat stream.");
                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var reader = new StreamReader(stream))
                            using (attempt.Token.Register(() => stream.Dispose()))
                            {
                                string eventName = null;
                                string payload = null;
                                string line;
                                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null && !attempt.IsCancellationRequested)
                                {
                                    attempt.CancelAfter(TimeSpan.FromSeconds(40));
                                    if (line.StartsWith("event:", StringComparison.Ordinal)) eventName = line.Substring(6).Trim();
                                    else if (line.StartsWith("data:", StringComparison.Ordinal) && line.Length < 65536) payload = line.Substring(5);
                                    else if (line.Length == 0)
                                    {
                                        if (eventName == "chat" && payload != null && !lifetime.IsCancellationRequested &&
                                            IsChzzkMessage(payload)) Interlocked.Exchange(ref pendingChat, 1);
                                        eventName = payload = null;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (HttpRequestException) { }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
                if (lifetime.IsCancellationRequested) return;
                try { await Task.Delay(3000, lifetime).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        static bool IsChzzkMessage(string payload)
        {
            try { return (string)JObject.Parse(payload)["platform"] == "chzzk"; }
            catch (Newtonsoft.Json.JsonException) { return false; }
        }

        void UpdateColors()
        {
            ApplyColor(bodyMaterial, BodyColor);
            ApplyColor(cheekMaterial, BlushColor);
            ApplyColor(eyeMaterial, EyeColor);
        }

        static void ApplyColor(Material material, Color color)
        {
            color.a = 1f;
            if (material != null && material.color != color) material.color = color;
        }

        static Vector2 Angles(Quaternion rotation)
        {
            var f = rotation * Vector3.forward;
            return new Vector2(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg);
        }

        static Vector2 BoneAngles(Quaternion[] rotations, HumanBodyBones bone)
        {
            int index = (int)bone;
            if (rotations == null || index >= rotations.Length) return Vector2.zero;
            return Angles(rotations[index]);
        }

        Vector3 Gaze(float now, bool expressionDetected, IReadOnlyDictionary<string, float> shapes, Quaternion[] rotations)
        {
            Vector2 headTurn = BoneAngles(rotations, HumanBodyBones.Head);
            Vector2 eyeTurn = (BoneAngles(rotations, HumanBodyBones.LeftEye) + BoneAngles(rotations, HumanBodyBones.RightEye)) * .5f;
            var tracked = new Vector2(
                Pair(shapes, "eyeLookOutRight", "eyeLookInLeft") - Pair(shapes, "eyeLookOutLeft", "eyeLookInRight"),
                Pair(shapes, "eyeLookDownLeft", "eyeLookDownRight") - Pair(shapes, "eyeLookUpLeft", "eyeLookUpRight")) * 40f;
            if (tracked.sqrMagnitude > eyeTurn.sqrMagnitude) eyeTurn = tracked;
            bool counterTurning = Vector2.Dot(headTurn, eyeTurn) < 0f;
            var direction = headTurn + eyeTurn * (counterTurning && headTurn.sqrMagnitude > eyeTurn.sqrMagnitude ? .45f : 1f);
            var weightedEyes = eyeTurn * 1.25f;
            float sensitivity = Mathf.Clamp(GazeSensitivity, .5f, 2f);
            float weightedMagnitude = Mathf.Max((headTurn + weightedEyes).sqrMagnitude,
                Mathf.Max(headTurn.sqrMagnitude, weightedEyes.sqrMagnitude)) * sensitivity * sensitivity;
            if (expressionDetected)
            {
                following = false;
                gazeStart = -1f;
            }
            else if (following)
            {
                following = weightedMagnitude > 10f * 10f;
                if (!following) gazeStart = -1f;
            }
            else if (weightedMagnitude >= 16f * 16f)
            {
                if (gazeStart < 0f) gazeStart = now;
                following = now - gazeStart >= Mathf.Clamp(GazeDelay, 0f, 2f);
            }
            else
            {
                gazeStart = -1f;
            }
            if (expressionDetected || following)
            {
                nextLook = now + 1.8f;
                idleAngles = Vector3.zero;
                return ClampGaze(new Vector3(direction.y, direction.x, 0));
            }
            if (weightedMagnitude < 16f * 16f)
            {
                Vector2 naturalDirection = headTurn + eyeTurn;
                var centerTarget = ClampGaze(new Vector3(naturalDirection.y, naturalDirection.x, 0));
                gazeCenter = Vector3.Lerp(gazeCenter, centerTarget, 1f - Mathf.Exp(-Time.deltaTime * 1.7f));
            }
            if (now >= nextLook)
            {
                bool wasCentered = idleAngles == Vector3.zero;
                bool center = !wasCentered && UnityEngine.Random.value < .44f;
                if (center)
                {
                    idleAngles = Vector3.zero;
                    nextLook = now + UnityEngine.Random.Range(1.8f, 4.4f);
                }
                else
                {
                    float side = UnityEngine.Random.value < .5f ? -1f : 1f;
                    float yaw = side * UnityEngine.Random.Range(13f, 29f);
                    float pitch = UnityEngine.Random.Range(-16f, 19f);
                    if (!wasCentered && Mathf.Abs(yaw - idleAngles.y) < 10f && Mathf.Abs(pitch - idleAngles.x) < 10f)
                        yaw = -yaw;
                    idleAngles = new Vector3(pitch, yaw, yaw * .22f);
                    nextLook = now + UnityEngine.Random.Range(.6f, 1.65f);
                }
            }
            return ClampGaze(gazeCenter + idleAngles);
        }
        static Vector3 ClampGaze(Vector3 angles)
        {
            return new Vector3(Mathf.Clamp(angles.x, -25f, 27f), Mathf.Clamp(angles.y, -35f, 35f), angles.z);
        }

        static float Value(IReadOnlyDictionary<string, float> shapes, string channel)
        {
            return shapes != null && shapes.TryGetValue(channel, out float value) ? Mathf.Clamp01(value) : 0f;
        }

        static float Pair(IReadOnlyDictionary<string, float> shapes, string left, string right)
        {
            return (Value(shapes, left) + Value(shapes, right)) * .5f;
        }
        float CurrentWeight(float smile, float sad)
        {
            switch (mood)
            {
                case Mood.Smile: return smile;
                case Mood.Sad: return sad;

                default: return 0f;
            }
        }

        void UpdateEyes(float now, float dt, float reading)
        {
            if (now >= nextBlink)
            {
                blinkStart = now;
                nextBlink = now + UnityEngine.Random.Range(2.8f, 5.5f);
            }
            float blink = Mathf.Clamp01(1f - Mathf.Abs((now - blinkStart - .075f) / .075f));
            bool smiling = mood == Mood.Smile;
            bool showRound = mood == Mood.Neutral && reading <= .01f && roundEyes > .08f;
            leftRenderer.enabled = rightRenderer.enabled = !smiling && !showRound;
            leftRound.enabled = rightRound.enabled = showRound;
            if (showRound)
            {
                var roundScale = new Vector3(Mathf.Lerp(.021f, .026f, roundEyes),
                    Mathf.Lerp(.054f, .026f, roundEyes) * Mathf.Lerp(1f, .22f, blink), .013f);
                leftRound.transform.localScale = rightRound.transform.localScale = roundScale;
            }
            leftArc.enabled = rightArc.enabled = smiling;
            float tilt = mood == Mood.Sad ? -68f : mood == Mood.Angry ? 62f : 0f;
            float length = mood == Mood.Sad ? .012f : mood == Mood.Angry ? .014f : .018f;
            float width = mood == Mood.Angry ? .011f : .014f;
            if (mood == Mood.Neutral) length *= Mathf.Lerp(1f, .65f, reading);
            var scale = new Vector3(width * EyeSize, Mathf.Lerp(length, .0035f, blink) * EyeSize, .013f);
            PoseEye(leftEye, tilt, scale, dt);
            PoseEye(rightEye, -tilt, scale, dt);
        }

        static void PoseEye(Transform eye, float tilt, Vector3 scale, float dt)
        {
            eye.localRotation = Quaternion.Slerp(eye.localRotation, Quaternion.Euler(0, 0, tilt), 1f - Mathf.Exp(-dt * 19f));
            eye.localScale = scale;
        }

        static MeshRenderer MakeArc(Transform eye, Material material)
        {
            var go = new GameObject("Reactive Smile Arc");
            go.transform.SetParent(eye.parent, false);
            var eyePosition = eye.localPosition;
            go.transform.localPosition = new Vector3(Mathf.Sign(eyePosition.x) * .0365f, eyePosition.y, eyePosition.z + .009f);
            const int segments = 20, sides = 12, capSteps = 4;
            const float width = .0125f, height = .012f;
            const int rings = segments + 1 + 2 * capSteps;
            var vertices = new Vector3[rings * sides];
            var triangles = new int[(rings - 1) * sides * 6];
            int index = 0;
            for (int i = 0; i < rings; i++)
            {
                int arcStep = i - capSteps;
                float a = Mathf.Clamp(arcStep, 0, segments) * Mathf.PI / segments;
                float x = Mathf.Cos(a), y = Mathf.Sin(a);
                var radial = new Vector3(x / width, y / height, 0).normalized;
                var tangent = new Vector3(-y * width, x * height, 0).normalized;
                var center = new Vector3(x * width, y * height - .008f, 0);
                float radius = Mathf.Lerp(.00656f, .00736f, y);
                float cap = (arcStep - Mathf.Clamp(arcStep, 0, segments)) * Mathf.PI / (2 * capSteps);
                center += tangent * (radius * Mathf.Sin(cap));
                radius *= Mathf.Max(0f, Mathf.Cos(cap));
                for (int j = 0; j < sides; j++)
                {
                    float b = j * 2f * Mathf.PI / sides;
                    int v = i * sides + j;
                    vertices[v] = center + radius * (radial * Mathf.Cos(b) + Vector3.forward * (.45f * Mathf.Sin(b)));
                    if (i == rings - 1) continue;
                    int next = i * sides + (j + 1) % sides;
                    triangles[index++] = v;
                    triangles[index++] = v + sides;
                    triangles[index++] = next;
                    triangles[index++] = next;
                    triangles[index++] = v + sides;
                    triangles[index++] = next + sides;
                }
            }
            var mesh = new Mesh { name = "Cat smile eye" };
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = Vector3.Scale(vertices[i], new Vector3(EyeSize, EyeSize, 1f));
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = false;
            return renderer;
        }

        MeshRenderer MakeCheek(Transform eyes, float x, Material material)
        {
            var go = new GameObject("Peach Cheek");
            go.transform.SetParent(eyes, false);
            const int steps = 32;
            var vertices = new Vector3[steps + 1];
            var triangles = new int[steps * 3];

            Vector3 Project(float u, float v)
            {
                var point = body.InverseTransformPoint(eyes.TransformPoint(new Vector3(x + u, -.027f + v, 0)));
                float horizontal = point.x / .128f;
                float vertical = point.y / .103f;
                point.z = .115f * Mathf.Sqrt(Mathf.Max(0f, 1f - horizontal * horizontal - vertical * vertical)) + .0014f;
                return eyes.InverseTransformPoint(body.TransformPoint(point));
            }

            vertices[0] = Project(0, 0);
            for (int i = 0; i < steps; i++)
            {
                float angle = i * 2f * Mathf.PI / steps;
                vertices[i + 1] = Project(.020f * Mathf.Cos(angle), .010f * Mathf.Sin(angle));
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % steps + 1;
            }
            var mesh = new Mesh { name = "Cat cheek blush", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
        static void DestroyMesh(MeshRenderer arc)
        {
            if (arc == null) return;
            UnityEngine.Object.Destroy(arc.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.Destroy(arc.gameObject);
        }
        void Unbind()
        {
            if (leftRound != null) UnityEngine.Object.Destroy(leftRound.gameObject);
            if (rightRound != null) UnityEngine.Object.Destroy(rightRound.gameObject);
            DestroyMesh(leftArc);
            DestroyMesh(rightArc);
            DestroyMesh(leftCheek);
            DestroyMesh(rightCheek);
            if (bodyRenderer != null) bodyRenderer.sharedMaterial = originalBody;
            if (leftRenderer != null) leftRenderer.sharedMaterial = originalLeftEye;
            if (rightRenderer != null) rightRenderer.sharedMaterial = originalRightEye;
            if (bodyMaterial != null) UnityEngine.Object.Destroy(bodyMaterial);
            if (eyeMaterial != null) UnityEngine.Object.Destroy(eyeMaterial);
            if (cheekMaterial != null) UnityEngine.Object.Destroy(cheekMaterial);
            bodyMaterial = eyeMaterial = cheekMaterial = null;
            bodyRenderer = null;
            originalBody = originalLeftEye = originalRightEye = null;
            if (leftRenderer != null) leftRenderer.enabled = true;
            if (rightRenderer != null) rightRenderer.enabled = true;
            if (leftEye != null) leftEye.localPosition = new Vector3(-.022f, 0, 0);
            if (rightEye != null) rightEye.localPosition = new Vector3(.022f, 0, 0);
            StopChat();
            overlayRequested = false;
            Interlocked.Exchange(ref pendingChat, 0);
            if (body != null)
            {
                body.localPosition = Vector3.zero;
                body.localScale = originalScale;
            }
            if (catAnimator != null) catAnimator.enabled = animatorWasEnabled;
            catRoot = null;
            catAnimator = null;
            body = leftEye = rightEye = null;
            receiver = null;
            trackingWasActive = false;
        }

        protected override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
        }
    }

    [NodeType(Id = "653a19bd-ae1e-4a8b-b3c4-ae2e4f006edb", Title = "Peach Cat Read Chat", Category = "Peach Cat")]
    public sealed class CatChatReadingNode : Node
    {
        [FlowInput]
        public Continuation Enter()
        {
            Context.PluginManager.GetPlugin<CatReactions>()?.QueueBlueprintChat();
            return Exit;
        }

        [FlowOutput]
        public Continuation Exit;
    }
}
