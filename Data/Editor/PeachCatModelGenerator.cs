using UnityEditor;
using UnityEngine;

public static class PeachCatModelGenerator
{
    // Face dimensions are radii in Unity units before the prefab's 0.5 scale.
    const float FaceWidth = .128f, FaceHeight = .103f, FaceDepth = .115f;
    const float EarHeight = .097f, EarOffset = .068f, EarTilt = 9f;

    [MenuItem("Tools/Peach Cat/Generate Editable Prefab")]
    static void GeneratePrefab()
    {
        Debug.Log("[Peach Cat] " + Generate());
    }

    static string Generate()
    {
        const string folder = "Assets/PeachCatEditableGenerated";
        if (AssetDatabase.IsValidFolder(folder)) return "Prototype folder already exists";
        if (Shader.Find("lilToon") == null) return "Install lilToon before generating the cat.";
        AssetDatabase.CreateFolder("Assets", "PeachCatEditableGenerated");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            System.Func<string, Transform, GameObject> Group = (name, parent) =>
            {
                var obj = new GameObject(name);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, scene);
                if (parent != null) obj.transform.SetParent(parent, false);
                return obj;
            };
            System.Func<string, Color, Material> Paint = (name, color) =>
            {
                var mat = new Material(Shader.Find(name == "Ink" ? "Unlit/Color" : "lilToon"));
                mat.color = color;
                if (name != "Ink")
                {
                    mat.SetFloat("_UseShadow",1f);
                    mat.SetFloat("_ShadowBorder",.43f);
                    mat.SetFloat("_ShadowBlur",.09f);
                    mat.SetFloat("_ShadowStrength",.72f);
                    mat.SetColor("_ShadowColor",new Color(.67f,.50f,.42f,1f));
                    mat.SetFloat("_LightMinLimit",.55f);
                    mat.SetFloat("_LightMaxLimit",1f);
                }
                AssetDatabase.CreateAsset(mat, folder + "/" + name + ".mat");
                return mat;
            };
            System.Action<string, Transform, PrimitiveType, Vector3, Vector3, float, Material> Shape = (name, parent, type, pos, scale, tilt, mat) =>
            {
                var obj = GameObject.CreatePrimitive(type);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, scene);
                obj.name = name;
                obj.transform.SetParent(parent, false);
                obj.transform.localPosition = pos;
                obj.transform.localScale = scale;
                obj.transform.localRotation = Quaternion.Euler(0, 0, tilt);
                UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
                obj.GetComponent<Renderer>().sharedMaterial = mat;
            };
            var blue = Paint("Peach", new Color32(255, 205, 152, 255));
            var ink = Paint("Ink", new Color32(8, 12, 19, 255));
            var root = Group("Prop", null);
            var body = Group("Bounce", root.transform).transform; body.localScale = Vector3.one * .5f; body.localRotation = Quaternion.Euler(0f,-7f,0f);
            var earShape = new[] {new Vector3(-.04667f,-.012f,0),new Vector3(.04667f,-.012f,0),new Vector3(0, EarHeight, 0),new Vector3(0,.045f,.020f),new Vector3(0,.045f,-.020f)};
            var faces = new[] {0,1,3,1,2,3,2,0,3,1,0,4,2,1,4,0,2,4};
            var planes = new Plane[2,6];
            for (int ear = 0; ear < 2; ear++)
            {
                int side = ear == 0 ? -1 : 1;
                var rotation = Quaternion.Euler(0,0,-side * EarTilt);
                var origin = new Vector3(side * EarOffset, .052f, 0);
                var points = new Vector3[5];
                var center = Vector3.zero;
                for (int n=0;n<5;n++)
                {
                    points[n] = origin + rotation * (earShape[n] * 1.12f);
                    center += points[n] / 5f;
                }
                for (int n=0;n<6;n++)
                {
                    Vector3 a = points[faces[n*3]];
                    Vector3 b = points[faces[n*3+1]];
                    Vector3 c = points[faces[n*3+2]];
                    var plane = new Plane(a,b,c);
                    planes[ear,n] = plane.GetDistanceToPoint(center) <= 0 ? plane : new Plane(a,c,b);
                }
            }
            System.Func<Vector3,float> Field = p =>
            {
                float d = (new Vector3(p.x / FaceWidth, p.y / FaceHeight, p.z / FaceDepth).magnitude-1f)*.115f;
                for (int ear=0;ear<2;ear++)
                {
                    float e = -1f;
                    for (int n=0;n<6;n++)
                    {
                        float planeDistance=planes[ear,n].GetDistanceToPoint(p);
                        const float round=.017f;
                        float edge=Mathf.Max(round-Mathf.Abs(e-planeDistance),0f)/round;
                        e=Mathf.Max(e,planeDistance)+edge*edge*round*.25f;
                    }
                    const float blend = .009f;
                    float h = Mathf.Max(blend-Mathf.Abs(d-e),0f)/blend;
                    d = Mathf.Min(d,e)-h*h*blend*.25f;
                }
                return d;
            };
            const int nx=80, ny=88, nz=62;
            const float step=.004f;
            var lower = new Vector3(-.160f,-.124f,-.124f);
            var samples = new float[nx+1,ny+1,nz+1];
            for (int x=0;x<=nx;x++)
            for (int y=0;y<=ny;y++)
            for (int z=0;z<=nz;z++)
                samples[x,y,z] = Field(lower+new Vector3(x,y,z)*step);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();
            var offsets = new[] {new Vector3Int(0,0,0),new Vector3Int(1,0,0),new Vector3Int(1,1,0),new Vector3Int(0,1,0),new Vector3Int(0,0,1),new Vector3Int(1,0,1),new Vector3Int(1,1,1),new Vector3Int(0,1,1)};
            int[,] tetra = {{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
            var positions = new Vector3[8];
            var cornerValues = new float[8];
            var inside = new int[4];
            var outside = new int[4];
            System.Func<int,int,Vector3> Edge = (a,b) => Vector3.LerpUnclamped(positions[a],positions[b],cornerValues[a]/(cornerValues[a]-cornerValues[b]));
            System.Func<Vector3,Vector3> Normal = p =>
            {
                const float e=.0005f;
                var dx=Vector3.right*e; var dy=Vector3.up*e; var dz=Vector3.forward*e;
                return new Vector3(Field(p+dx)-Field(p-dx),Field(p+dy)-Field(p-dy),Field(p+dz)-Field(p-dz)).normalized;
            };
            var lookup=new System.Collections.Generic.Dictionary<Vector3Int,int>();
            System.Func<Vector3,int> AddVertex = p =>
            {
                var key=Vector3Int.RoundToInt(p*1000000f);
                int index;
                if(lookup.TryGetValue(key,out index)) return index;
                index=vertices.Count;
                lookup.Add(key,index);
                vertices.Add(p);
                normals.Add(Normal(p));
                return index;
            };
            System.Action<Vector3,Vector3,Vector3> Triangle = (a,b,c) =>
            {
                int ia=AddVertex(a), ib=AddVertex(b), ic=AddVertex(c);
                if(ia==ib || ib==ic || ic==ia) return;
                bool forward=Vector3.Dot(Vector3.Cross(b-a,c-a),normals[ia]+normals[ib]+normals[ic])>0;
                indices.Add(ia); indices.Add(forward?ib:ic); indices.Add(forward?ic:ib);
            };
            for (int x=0;x<nx;x++)
            for (int y=0;y<ny;y++)
            for (int z=0;z<nz;z++)
            {
                int count=0;
                for(int n=0;n<8;n++)
                {
                    var offset=offsets[n];
                    cornerValues[n]=samples[x+offset.x,y+offset.y,z+offset.z];
                    if(cornerValues[n]<0) count++;
                }
                if(count==0 || count==8) continue;
                for(int n=0;n<8;n++) positions[n]=lower+(new Vector3(x,y,z)+(Vector3)offsets[n])*step;
                for(int tIndex=0;tIndex<6;tIndex++)
                {
                    int a=0,b=0;
                    for(int n=0;n<4;n++)
                    {
                        int corner=tetra[tIndex,n];
                        if(cornerValues[corner]<0) inside[a++]=corner; else outside[b++]=corner;
                    }
                    if(a==1) Triangle(Edge(inside[0],outside[0]),Edge(inside[0],outside[1]),Edge(inside[0],outside[2]));
                    if(a==3) Triangle(Edge(outside[0],inside[0]),Edge(outside[0],inside[1]),Edge(outside[0],inside[2]));
                    if(a==2)
                    {
                        var p=Edge(inside[0],outside[0]); var q=Edge(inside[0],outside[1]);
                        var r=Edge(inside[1],outside[0]); var u=Edge(inside[1],outside[1]);
                        Triangle(p,q,r); Triangle(q,u,r);
                    }
                }
            }
            var mesh = new Mesh {name="Blended cat silhouette", indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateBounds();
        var celNormals=mesh.normals;
        var uv=new Vector2[celNormals.Length];
        var light=new Vector3(-.35f,.68f,.65f).normalized;
        for(int i=0;i<uv.Length;i++) uv[i]=new Vector2(.5f+.5f*Vector3.Dot(celNormals[i],light),.5f);
        mesh.uv=uv;
        var ramp=new Texture2D(128,1,TextureFormat.RGBA32,false);
        ramp.name="Soft cel tone";
        ramp.wrapMode=TextureWrapMode.Clamp;
        ramp.filterMode=FilterMode.Bilinear;
        for(int x=0;x<128;x++)
        {
            float rampValue=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,.50f,x/127f));
            ramp.SetPixel(x,0,Color.Lerp(new Color(.91f,.84f,.80f,1),Color.white,rampValue));
        }
        ramp.Apply();
        AssetDatabase.CreateAsset(ramp,folder+"/CelTone.asset"); blue.mainTexture=ramp; EditorUtility.SetDirty(blue);
            AssetDatabase.CreateAsset(mesh, folder+"/CatBody.asset");
            var shell=Group("Face and ears",body);
            shell.AddComponent<MeshFilter>().sharedMesh=mesh;
            shell.AddComponent<MeshRenderer>().sharedMaterial=blue;
            var pivot = Group("Gaze", body).transform; var eyes = Group("Eyes", pivot).transform;
            eyes.localPosition = new Vector3(0,0,.115f);
            Shape("Eye L", eyes, PrimitiveType.Capsule, new Vector3(-.022f,0,0), new Vector3(.014f,.018f,.013f), 0, ink);
            Shape("Eye R", eyes, PrimitiveType.Capsule, new Vector3(.022f,0,0), new Vector3(.014f,.018f,.013f), 0, ink);
            pivot.localRotation = Quaternion.Euler(-11f,0f,0f); var clip = new AnimationClip { name = "Look Around", frameRate = 30 };
            float[] t = {0f,.53f,.77f,1.37f,1.61f,2.08f,2.34f,2.89f,3.22f,3.78f,4.06f,4.8f};
            float[] pitch = {0f,0f,19f,19f,29f,29f,18f,18f,-2f,-2f,0f,0f};
            float[] yaw = {-7f,-7f,-27f,-27f,-8f,-8f,15f,15f,4f,4f,-7f,-7f};
            float[] roll = {0f,0f,-16f,-16f,-3f,-3f,17f,17f,5f,5f,0f,0f};
            System.Action<string, string, float[], float[]> Curve = (path, property, times, values) =>
            {
                var keys = new Keyframe[times.Length];
                for (int j=0;j<times.Length;j++) keys[j] = new Keyframe(times[j], values[j], 0, 0);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), new AnimationCurve(keys));
            };
            Curve("Bounce", "localEulerAnglesRaw.x", t, pitch);
            Curve("Bounce", "localEulerAnglesRaw.y", t, yaw);
            Curve("Bounce", "localEulerAnglesRaw.z", t, roll);
            float[] blinkTimes = {0f,1.42f,1.49f,1.53f,1.63f,3.8f,3.87f,3.91f,4.01f,4.8f};
            float[] blink = {.018f,.018f,.004f,.004f,.018f,.018f,.004f,.004f,.018f,.018f};
            Curve("Bounce/Gaze/Eyes/Eye L", "m_LocalScale.y", blinkTimes, blink);
            Curve("Bounce/Gaze/Eyes/Eye R", "m_LocalScale.y", blinkTimes, blink);
            foreach (var path in new[] {"Bounce/Gaze/Eyes/Eye L", "Bounce/Gaze/Eyes/Eye R"})
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.x"), AnimationCurve.Constant(0, 4.8f, .014f));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.z"), AnimationCurve.Constant(0, 4.8f, .013f));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            AssetDatabase.CreateAsset(clip, folder + "/LookAround.anim");
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(folder + "/Cat.controller");
            controller.AddMotion(clip);
            root.AddComponent<Animator>().runtimeAnimatorController = controller;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Prop.prefab");
            AssetDatabase.SaveAssets();
            return prefab != null ? AssetDatabase.GetAssetPath(prefab) : "Prefab save failed";
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}
