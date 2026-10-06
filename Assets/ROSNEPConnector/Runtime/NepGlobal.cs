using System;
using UnityEngine;
using Nep;
using NepStdMsgs;
using NepSensorMsgs;
using NepGeometryMsgs;

namespace NepStdMsgs
{
    public class NepHeader
    {
        public Int32 seq { set; get; }
        public string frame_id { set; get; }
        public NepSensorMsgs.Time stamp { set; get; }
    }

    public class NepDuration
    {
        public Int32 sec { set; get; }
        public Int32 nsec { set; get; }
    }

    public class NepColorRGBA
    {
        public float r { set; get; }
        public float g { set; get; }
        public float b { set; get; }
        public float a { set; get; }
    }
}

namespace NepGeometryMsgs
{
    public class NepPoseStamped
    {
        public NepPoseStamped()
        {
            header = new NepHeader();
            pose = new NepPose();
            pose.position = new NepPoint();
            pose.orientation = new NepQuaternion();
        }

        public NepHeader header { get; set; }
        public NepPose pose { get; set; }
    }

    public class NepTransformStamped
    {
        public NepTransformStamped()
        {
            header = new NepHeader();
            transform = new NepTransform();
            transform.translation = new NepVector3();
            transform.rotation = new NepQuaternion();
        }

        public NepHeader header { set; get; }
        public string child_frame_id { set; get; }
        public NepTransform transform { set; get; }
    }

    public class NepWrenchStamped
    {
        public NepWrenchStamped()
        {
            header = new NepHeader();
            wrench = new NepWrench();
            wrench.force = new NepVector3();
            wrench.torque = new NepVector3();
        }

        public NepHeader header { get; set; }
        public NepWrench wrench { get; set; }        
    }
}

namespace NepShapeMsgs
{
    public class NepMeshTriangle
    {
        public uint[] vertex_indices { get; set; }

        NepMeshTriangle()
        {
            vertex_indices = new uint[3];
        }
    }

    public class NepMesh
    {
        public NepMeshTriangle[] triangles { get; set; }
        public NepPoint[] vertices { get; set; }
    }

    public class NepSolidPrimitive
    {
        public byte type { set; get; }
        public double[] dimensions { set; get; }
    }
}

namespace NepVisualizationMsgs
{
    public class NepMarker
    {
        public NepMarker()
        {
            header = new NepHeader();
            pose = new NepPose();
            scale = new NepVector3();
            color = new NepColorRGBA();
            lifetime = new NepDuration();

            color.r = color.g = color.b = color.a = 1;
        }

        public NepHeader header { set; get; }
        public string ns { set; get; }
        public Int32 id { set; get; }
        public Int32 type { set; get; }
        public Int32 action { set; get; }
        public NepPose pose { set; get; }
        public NepVector3 scale { set; get; }
        public NepColorRGBA color { set; get; }
        public NepDuration lifetime { set; get; }
        public bool frame_locked { set; get; }
        public NepPoint[] points { set; get; }
        public NepColorRGBA[] colors { set; get; }
        public string text { set; get; }
        public string mesh_resource { set; get; }
        public bool mesh_use_embeded_materials { set; get; }
    }

    public class NepMarkerArray
    {
        public NepMarker[] markers;
    }
}

namespace NepTF2Msgs
{
    public class NepTFMessage
    {
        public NepTransformStamped[] transforms { set; get; }
    }
}

namespace ROSNEPConnector
{
    public class NepGlobal : SingletonMonoBehaviour<NepGlobal>
    {
        public static Vector3 NepToUnity(NepVector3 nep_vector)
        {
            var vector = new Vector3();
            vector.x = (float)nep_vector.x;
            vector.y = (float)nep_vector.z;
            vector.z = (float)nep_vector.y;

            return vector;
        }

        public static Vector3 NepToUnity(NepPoint nep_point)
        {
            var point = new Vector3();
            point.x = (float)nep_point.x;
            point.y = (float)nep_point.z;
            point.z = (float)nep_point.y;

            return point;
        }

        public static Quaternion NepToUnity(NepQuaternion nep_quaternion)
        {
            var quaternion = new Quaternion();
            quaternion.x = (float)nep_quaternion.x;
            quaternion.y = (float)nep_quaternion.z;
            quaternion.z = (float)nep_quaternion.y;
            quaternion.w = -(float)nep_quaternion.w;

            return quaternion;
        }

        public static Pose NepToUnity(NepPose nep_pose)
        {
            var pose = new Pose();
            pose.position = NepToUnity(nep_pose.position);
            pose.rotation = NepToUnity(nep_pose.orientation);

            return pose;
        }

        public static NepVector3 UnityToNep(Vector3 vector)
        {
            var nep_vector = new NepVector3();
            nep_vector.x = (double)vector.x;
            nep_vector.y = (double)vector.z;
            nep_vector.z = (double)vector.y;

            return nep_vector;
        }

        public static NepPoint UnityToNepPoint(Vector3 point)
        {
            var nep_point = new NepPoint();
            nep_point.x = (double)point.x;
            nep_point.y = (double)point.z;
            nep_point.z = (double)point.y;

            return nep_point;
        }

        public static NepQuaternion UnityToNep(Quaternion quaternion)
        {
            var nep_quaternion = new NepQuaternion();
            nep_quaternion.x = (double)quaternion.x;
            nep_quaternion.y = (double)quaternion.z;
            nep_quaternion.z = (double)quaternion.y;
            nep_quaternion.w = -(double)quaternion.w;

            return nep_quaternion;
        }

        public static NepPose UnityToNep(Pose pose)
        {
            var nep_pose = new NepPose();
            nep_pose.position = UnityToNepPoint(pose.position);
            nep_pose.orientation = UnityToNep(pose.rotation);

            return nep_pose;
        }

        // Member variables
        [SerializeField] private string ipAddress;   // IP address of NEP master
        private NepNode _node;
        private NepConfiguration _conf;

        // Member functions
        protected override void Awake()
        {
            _node = new NepNode("unity_test");
            _conf = _node.Hybrid(ipAddress);
        }

        private void Update()
        {
            _node.Update();
        }

        protected override void OnDestroy()
        {
            _node.Close();
            _node.Dispose();
        }

        public static string IP { get { return Instance.ipAddress; } }
        public static NepNode Node { get { return Instance._node; } }
        public static NepConfiguration Conf { get { return Instance._conf; } }
    }
}