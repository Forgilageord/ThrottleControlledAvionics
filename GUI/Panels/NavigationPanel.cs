﻿//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution-ShareAlike 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by-sa/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
//
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace ThrottleControlledAvionics
{
	public class NavigationPanel : ControlPanel
	{
		Vessel vessel { get { return TCA.vessel; } }

		public NavigationPanel(ModuleTCA tca) : base(tca) 
		{ RenderingManager.AddToPostDrawQueue(1, WaypointOverlay); }

		public override void Reset()
		{ RenderingManager.RemoveFromPostDrawQueue(1, WaypointOverlay); }

		AutoLander LND;
		PointNavigator PN;

		bool selecting_target;
		bool select_single;
		Vector2 waypointsScroll;
		readonly ActionDamper AddTargetDamper = new ActionDamper();
		const string WPM_ICON = "ThrottleControlledAvionics/Icons/waypoint";
		const string PN_ICON  = "ThrottleControlledAvionics/Icons/path-node";
		const float  IconSize = 16;
		static Texture2D WayPointMarker, PathNodeMarker;

		public static void OnAwake()
		{
			WayPointMarker = GameDatabase.Instance.GetTexture(WPM_ICON, false);
			PathNodeMarker = GameDatabase.Instance.GetTexture(PN_ICON, false);
		}

		public override void Draw()
		{
			if(PN == null || !VSL.OnPlanet) return;
			GUILayout.BeginHorizontal();
			if(VSL.HasTarget && !CFG.Nav.Paused)
			{
				if(GUILayout.Button(new GUIContent("Go To", "Fly to current target"), 
				                        CFG.Nav[Navigation.GoToTarget]? Styles.green_button 
				                        : Styles.yellow_button,
				                        GUILayout.Width(50)))
				{
					CFG.Nav.XOn(Navigation.GoToTarget);
					if(CFG.Nav[Navigation.GoToTarget]) follow_me();
				}
				if(GUILayout.Button(new GUIContent("Follow", "Follow current target"), 
				                        CFG.Nav[Navigation.FollowTarget]? Styles.green_button 
				                        : Styles.yellow_button,
				                        GUILayout.Width(50)))
					apply(tca => 
				{
					if(TCA.vessel.targetObject as Vessel == tca.vessel) return;
					tca.vessel.targetObject = TCA.vessel.targetObject;
					tca.CFG.Nav.XOn(Navigation.FollowTarget);
				});
			}
			else 
			{
				GUILayout.Label(new GUIContent("Go To", CFG.Nav.Paused? "Paused" : "No target selected"),  
				                    Styles.grey, GUILayout.Width(50));
				GUILayout.Label(new GUIContent("Follow", CFG.Nav.Paused? "Paused" : "No target selected"),  
				                    Styles.grey, GUILayout.Width(50));
			}
			if(SQD != null && SQD.SquadMode)
			{
				if(CFG.Nav.Paused)
					GUILayout.Label(new GUIContent("Follow Me", "Make the squadron follow"),  
					                    Styles.grey, GUILayout.Width(75));
				else if(GUILayout.Button(new GUIContent("Follow Me", "Make the squadron follow"), 
				                             Styles.yellow_button, GUILayout.Width(75)))
					follow_me();
			}
			if(selecting_target)
				selecting_target &= !GUILayout.Button("Cancel", Styles.red_button, GUILayout.Width(120));
			else if(VSL.HasTarget && 
			            !(VSL.Target is WayPoint) && 
			            (CFG.Waypoints.Count == 0 || VSL.Target != CFG.Waypoints.Peek().GetTarget()))
			{
				if(GUILayout.Button(new GUIContent("Add As Waypoint", "Add current target as a waypoint"), 
				                        Styles.yellow_button, GUILayout.Width(120)))
				{
					CFG.Waypoints.Enqueue(new WayPoint(VSL.Target));
					CFG.ShowWaypoints = true;
				}
			}
			else if(GUILayout.Button(new GUIContent("Add Waypoint", "Select a new waypoint"), 
			                             Styles.yellow_button, GUILayout.Width(120)))
			{
				selecting_target = true;
				CFG.ShowWaypoints = true;
			}
			if(CFG.Waypoints.Count > 0 && !CFG.Nav.Paused)
			{
				if(GUILayout.Button("Follow Route",
				                        CFG.Nav[Navigation.FollowPath]? Styles.green_button 
				                        : Styles.yellow_button,
				                        GUILayout.Width(90)))
				{
					CFG.Nav.XToggle(Navigation.FollowPath);
					if(CFG.Nav[Navigation.FollowPath])
						follow_me();
				}
			}
			else GUILayout.Label(new GUIContent("Follow Route", CFG.Nav.Paused? "Paused" : "Add some waypoints first"), 
			                         Styles.grey, GUILayout.Width(90));
			var max_nav_speed = Utils.FloatSlider("", CFG.MaxNavSpeed, GLB.PN.MinSpeed, GLB.PN.MaxSpeed, "0 m/s", 60, "Maximum horizontal speed on autopilot");
			if(Mathf.Abs(max_nav_speed-CFG.MaxNavSpeed) > 1e-5)
				apply_cfg(cfg => cfg.MaxNavSpeed = max_nav_speed);
			GUILayout.EndHorizontal();
		}

		public void AddSingleWaypointInMapView()
		{
			if(selecting_target)
				selecting_target &= !GUILayout.Button("Cancel", Styles.red_button, GUILayout.Width(120));
			else if(GUILayout.Button(new GUIContent("Add Target", "Select target point"), 
			                         Styles.yellow_button, GUILayout.ExpandWidth(false)))
			{
				select_single = true;
				selecting_target = true;
				CFG.GUIVisible = true;
				CFG.ShowWaypoints = true;
				MapView.EnterMapView();
			}
		}

		public void WaypointList()
		{
			if(CFG.Waypoints.Count == 0) return;
			GUILayout.BeginVertical();
			if(GUILayout.Button(CFG.ShowWaypoints? "Hide Waypoints" : "Show Waypoints", 
			                    Styles.yellow_button,
			                    GUILayout.ExpandWidth(true)))
				CFG.ShowWaypoints = !CFG.ShowWaypoints;
			if(CFG.ShowWaypoints)
			{
				GUILayout.BeginVertical(Styles.white);
				waypointsScroll = GUILayout
					.BeginScrollView(waypointsScroll, 
					                 GUILayout.Height(Utils.ClampH(ThrottleControlledAvionics.LineHeight*(CFG.Waypoints.Count+1), 
					                                               ThrottleControlledAvionics.ControlsHeight)));
				GUILayout.BeginVertical();
				int i = 0;
				var num = (float)(CFG.Waypoints.Count-1);
				var del = new HashSet<WayPoint>();
				var col = GUI.contentColor;
				foreach(var wp in CFG.Waypoints)
				{
					GUILayout.BeginHorizontal();
					GUI.contentColor = marker_color(i, num);
					var label = string.Format("{0}) {1}", 1+i, wp.GetName());
					if(CFG.Nav[Navigation.FollowPath] && i == 0)
					{
						var d = wp.DistanceTo(vessel);
						label += string.Format(" <= {0}", Utils.DistanceToStr(d)); 
						if(vessel.horizontalSrfSpeed > 0.1)
							label += string.Format(", ETA {0:c}", new TimeSpan(0,0,(int)(d/vessel.horizontalSrfSpeed)));
					}
					if(GUILayout.Button(label,GUILayout.ExpandWidth(true)))
						FlightGlobals.fetch.SetVesselTarget(wp.GetTarget());
					GUI.contentColor = col;
					GUILayout.FlexibleSpace();
					if(LND != null && 
					   GUILayout.Button(new GUIContent("Land", "Land on arrival"), 
					                    wp.Land? Styles.green_button : Styles.yellow_button, 
					                    GUILayout.Width(50))) 
						wp.Land = !wp.Land;
					if(GUILayout.Button(new GUIContent("||", "Pause on arrival"), 
					                    wp.Pause? Styles.green_button : Styles.yellow_button, 
					                    GUILayout.Width(25))) 
						wp.Pause = !wp.Pause;
					if(GUILayout.Button(new GUIContent("X", "Delete waypoint"), 
					                    Styles.red_button, GUILayout.Width(25))) 
						del.Add(wp);
					GUILayout.EndHorizontal();
					i++;
				}
				GUI.contentColor = col;
				if(GUILayout.Button("Clear", Styles.red_button, GUILayout.ExpandWidth(true)))
					CFG.Waypoints.Clear();
				else if(del.Count > 0)
				{
					var edited = CFG.Waypoints.Where(wp => !del.Contains(wp)).ToList();
					CFG.Waypoints = new Queue<WayPoint>(edited);
				}
				if(CFG.Waypoints.Count == 0 && CFG.Nav) CFG.HF.XOn(HFlight.Stop);
				GUILayout.EndVertical();
				GUILayout.EndScrollView();
				GUILayout.EndVertical();
			}
			GUILayout.EndVertical();
		}

		#region Waypoints Overlay
		static Color marker_color(int i, float N)
		{ 
			if(N.Equals(0)) return Color.red;
			var t = i/N;
			return t < 0.5f ? 
				Color.Lerp(Color.red, Color.green, t*2).Normalized() : 
				Color.Lerp(Color.green, Color.cyan, (t-0.5f)*2).Normalized(); 
		}

		//adapted from MechJeb
		bool clicked;
		DateTime clicked_time;
		void WaypointOverlay()
		{
			if(TCA == null || !TCA.Available || !ThrottleControlledAvionics.showHUD) return;
			if(selecting_target)
			{
				var coords = MapView.MapIsEnabled? 
					Utils.GetMouseCoordinates(vessel.mainBody) :
					Utils.GetMouseFlightCoordinates();
				if(coords != null)
				{
					var t = new WayPoint(coords);
					DrawGroundMarker(vessel.mainBody, coords.Lat, coords.Lon, new Color(1.0f, 0.56f, 0.0f));
					GUI.Label(new Rect(Input.mousePosition.x + 15, Screen.height - Input.mousePosition.y, 200, 50), 
					          string.Format("{0} {1}\n{2}", coords, Utils.DistanceToStr(t.DistanceTo(vessel)), 
					                        ScienceUtil.GetExperimentBiome(vessel.mainBody, coords.Lat, coords.Lon)));
					if(!clicked)
					{ 
						if(Input.GetMouseButtonDown(0)) clicked = true;
						else if(Input.GetMouseButtonDown(1))  
						{ clicked_time = DateTime.Now; clicked = true; }
					}
					else 
					{
						if(Input.GetMouseButtonUp(0))
						{ 
							AddTargetDamper.Run(() => CFG.Waypoints.Enqueue(t));
							CFG.ShowWaypoints = true;
							clicked = false;
							if(select_single)
							{
								selecting_target = false;
								select_single = false;
								VSL.Target = t;
							}
						}
						if(Input.GetMouseButtonUp(1))
						{ 
							selecting_target &= (DateTime.Now - clicked_time).TotalSeconds >= 0.5;
							clicked = false; 
						}
					}
				}
			}
			if(CFG.ShowWaypoints)
			{
				var i = 0;
				var num = (float)(CFG.Waypoints.Count-1);
				WayPoint wp0 = null;
				foreach(var wp in CFG.Waypoints)
				{
					wp.UpdateCoordinates(vessel.mainBody);
					var c = marker_color(i, num);
					if(wp0 == null) DrawPath(vessel, wp, c);
					else DrawPath(vessel.mainBody, wp0, wp, c);
					DrawGroundMarker(vessel.mainBody, wp.Lat, wp.Lon, c);
					wp0 = wp; i++;
				}
			}
		}

		static Material _icon_material;
		static Material IconMaterial
		{
			get
			{
				if(_icon_material == null) 
					_icon_material = new Material(Shader.Find("Particles/Additive"));
				return _icon_material;
			}
		}

		static void DrawMarker(Vector3 icon_center, Color c, float r, Texture2D texture)
		{
			if(texture == null) texture = WayPointMarker;
			var icon_rect = new Rect(icon_center.x - r * 0.5f, (float)Screen.height - icon_center.y - r * 0.5f, r, r);
			Graphics.DrawTexture(icon_rect, texture, new Rect(0f, 0f, 1f, 1f), 0, 0, 0, 0, c, IconMaterial);
		}

		static void DrawGroundMarker(CelestialBody body, double lat, double lon, Color c, float r = IconSize, Texture2D texture = null)
		{
			Vector3d center;
			Camera camera;
			if(MapView.MapIsEnabled)
			{
				camera = PlanetariumCamera.Camera;
				//TODO: cache local center coordinates of the marker
				var up = body.GetSurfaceNVector(lat, lon);
				var h  = Utils.TerrainAltitude(body, lat, lon);
				if(h < body.Radius) h = body.Radius;
				center = body.position + h * up;
			}
			else
			{
				camera = FlightCamera.fetch.mainCamera;
				center = body.GetWorldSurfacePosition(lat, lon, Utils.TerrainAltitude(body, lat, lon)+GLB.WaypointHeight);
				if(Vector3d.Dot(center-camera.transform.position, 
				                camera.transform.forward) <= 0) return;
			}
			if(IsOccluded(center, body)) return;
			DrawMarker(camera.WorldToScreenPoint(MapView.MapIsEnabled? ScaledSpace.LocalToScaledSpace(center) : center), c, r, texture);
		}

		static void DrawPath(CelestialBody body, WayPoint wp0, WayPoint wp1, Color c)
		{
			var D = wp1.AngleTo(wp0);
			var N = (int)Mathf.Clamp((float)D*Mathf.Rad2Deg, 2, 5);
			var dD = D/N;
			for(int i = 1; i<N; i++)
			{
				var p = wp0.PointBetween(wp1, dD*i);
				DrawGroundMarker(body, p.Lat, p.Lon, c, IconSize/2, PathNodeMarker);
			}
		}

		static void DrawPath(Vessel v, WayPoint wp1, Color c)
		{
			var wp0 = new WayPoint();
			wp0.Lat = v.latitude; wp0.Lon = v.longitude;
			DrawPath(v.mainBody, wp0, wp1, c);
		}

		//Tests if byBody occludes worldPosition, from the perspective of the planetarium camera
		static bool IsOccluded(Vector3d worldPosition, CelestialBody byBody)
		{
			return Vector3d.Angle(ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position) - 
			                      worldPosition, byBody.position - worldPosition) <= 90.0;
		}
		#endregion
	}
}
