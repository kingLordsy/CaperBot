using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Communication.Clients;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace CaperBot {

	public class DraggableSlot {
		public Label label;
		public bool isQueue;
	}

	public class LiveSlot : DraggableSlot {
		//public Label label;
		public Label positionLabel;
		public Label usernameLabel;
		public Label redemptionTimeLabel;
		public Label liveTimeLabel;
		public Label attemptsLabel;
		public Label completionsLabel;
		public Label redemptionsLabel;
		public Button attemptsAddButton;
		public Button attemptsSubButton;
		public Button completionsAddButton;
		public Button completionsSubButton;
		public Button deleteSlotButton;

		private bool alive;
		public long redemptionTime;
		public long liveTime;
		public UserData userData;

		private Thread thread;
		private Thread threadsecond;

		public static List<LiveSlot> Live = new List<LiveSlot>();

		public static bool IsRoom() {
			return Live.Count < 4;
		}

		private static void UpdateSlotNumbers() {
			for (int i = 0; i < Live.Count; i++) {
				Live[i].positionLabel.Content = "#" + (i + 1).ToString();
			}
		}

		public static void Move(LiveSlot slot, int pos) {
			Live.Remove(slot);
			if (pos >= Live.Count) {
				Live.Add(slot);
			} else {
				Live.Insert(pos, slot);
			}
			UpdateSlotNumbers();
			RefreshLiveStack();
		}
		public static void RefreshLiveStack() {
			try {
				var children = MainWindow.Reference.LiveStack.Children;
				if (Live.Count == 0) {
					children.Clear();
					return;
				} else if (Live.Count > 4) {
					Live.RemoveRange(4, Live.Count - 4);
				}
				for (int i = 0; i < Live.Count && i < 4; i++) {
					if (children.Count <= i) {
						children.Add(Live[i].label);
					} else if (Live[i].label != children[i]) {
						children.Insert(i, Live[i].label);
					}
				}
				if (children.Count > Live.Count) {
					children.RemoveRange(Live.Count, children.Count - Live.Count);
				} else if (children.Count > 4) {
					children.RemoveRange(4, children.Count - 4);
				}
			} catch {
				var children = MainWindow.Reference.LiveStack.Children;
				children.Clear();
				for (int i = 0; i < Live.Count && i < 4; i++) {
					children.Add(Live[i].label);
				}
			}
		}

		public static void AddSlot(LiveSlot slot) {
			if (Live.Count > 3) return;
			Live.Add(slot);
			RefreshLiveStack();
			UpdateSlotNumbers();
		}

		public static void AddSlot(LiveSlot slot, int pos) {
			if (Live.Count > 3) return;
			if (pos >= Live.Count) {
				Live.Add(slot);
			} else {
				Live.Insert(pos, slot);
			}
			RefreshLiveStack();
			UpdateSlotNumbers();
		}


		public static void RemoveSlot(LiveSlot slot) {
			var at = Live.FindIndex(a => a == slot);
			if (at < 0) {
				return;
			}
			Live.RemoveAt(at);
			try {
				if (MainWindow.Reference.LiveStack.Children[at] == slot.label) {
					MainWindow.Reference.LiveStack.Children.RemoveAt(at);
				} else {
					RefreshLiveStack();
				}
			} catch {
				RefreshLiveStack();
			}
			UpdateSlotNumbers();
		}

		public static LiveSlot FromQueue(QueueSlot slot) {
			return FromQueue(slot, Live.Count);
		}

		public static LiveSlot FromQueue(QueueSlot slot, int pos) {
			if (pos > 3) {
				return null;
			}
			QueueSlot.RemoveSlot(slot);
			var ret = new LiveSlot();
			ret.isQueue = false;

			if (Live.Count > pos) {
				var temp = Live[pos];
				RemoveSlot(temp);
				new QueueSlot(temp.userData, temp.redemptionTime);
			}
			MainWindow.Reference.QueueStack.Dispatcher.Invoke(delegate {
				ret.CreateLiveLabel();
				ret.usernameLabel.Content = slot.userData.UserName;
				ret.redemptionTime = slot.redemptionTime;
				ret.liveTime = Utilities.Times.GetCurrentMillis();
				ret.userData = slot.userData;
				ret.UpdateAttemptsLabel();
				ret.UpdateCompletionsLabel();
				ret.UpdateRedemptionsLabel();

				ret.attemptsAddButton.Click += ret.AttemptsAddClick;
				ret.attemptsSubButton.Click += ret.AttemptsSubClick;
				ret.completionsAddButton.Click += ret.CompletionsAddClick;
				ret.completionsSubButton.Click += ret.CompletionsSubClick;

				ret.deleteSlotButton.Click += ret.Cleanup;
				ret.alive = true;

				ret.thread = new Thread(ret.Updater);
				ret.thread.Start();

				ret.threadsecond = new Thread(ret.LiveUpdater);
				ret.threadsecond.Start();

				AddSlot(ret, pos);
			});
			return ret;
		}

		private LiveSlot() { }

		public LiveSlot(UserData userData, long redemptionTime) {
			if (Live.Count > 3) {
				alive = false;
				return;
			}
			isQueue = false;
			MainWindow.Reference.LiveStack.Dispatcher.Invoke(delegate {
				CreateLiveLabel();
				usernameLabel.Content = userData.UserName;
				this.redemptionTime = redemptionTime;
				this.liveTime = Utilities.Times.GetCurrentMillis();
				this.userData = userData;
				UpdateAttemptsLabel();
				UpdateCompletionsLabel();
				UpdateRedemptionsLabel();

				attemptsAddButton.Click += AttemptsAddClick;
				attemptsSubButton.Click += AttemptsSubClick;
				completionsAddButton.Click += CompletionsAddClick;
				completionsSubButton.Click += CompletionsSubClick;

				deleteSlotButton.Click += Cleanup;
				alive = true;

				thread = new Thread(Updater);
				thread.Start();

				threadsecond = new Thread(LiveUpdater);
				threadsecond.Start();

				AddSlot(this);
			});
		}

		public void Updater() {
			redemptionTimeLabel.Dispatcher.Invoke(delegate {
				try {
					redemptionTimeLabel.Content = Utilities.Times.FormatTime(Utilities.Times.GetTimeSpent(redemptionTime) + 50, false);
				} catch { }
			});
			while (alive) {
				Thread.Sleep(1000 - (int)(Utilities.Times.GetTimeSpent(redemptionTime) % 1000));
				if (alive) {
					try {
						redemptionTimeLabel.Dispatcher.Invoke(delegate {
							try {
								redemptionTimeLabel.Content = Utilities.Times.FormatTime(Utilities.Times.GetTimeSpent(redemptionTime) + 50, false);
							} catch { }
						});
					} catch { }
				}
				Thread.Sleep(50);
			}
		}

		public void LiveUpdater() {
			liveTimeLabel.Dispatcher.Invoke(delegate {
				try {
					liveTimeLabel.Content = Utilities.Times.FormatTime(Utilities.Times.GetTimeSpent(liveTime) + 50, false);
				} catch { }
			});
			while (alive) {
				Thread.Sleep(1000 - (int)(Utilities.Times.GetTimeSpent(liveTime) % 1000));
				if (alive) {
					try {
						liveTimeLabel.Dispatcher.Invoke(delegate {
							try {
								liveTimeLabel.Content = Utilities.Times.FormatTime(Utilities.Times.GetTimeSpent(liveTime) + 50, false);
							} catch { }
						});
					} catch { }
				}
				Thread.Sleep(50);
			}
		}

		public void Cleanup() {
			alive = false;
			try {
				thread.Abort();
			} catch { }
			try {
				threadsecond.Abort();
			} catch { }
			RemoveSlot(this);
		}

		public void Cleanup(object sender, RoutedEventArgs args) {
			Cleanup();
		}

		public void AttemptsAddClick(object sender, RoutedEventArgs args) {
			userData.Attempts++;
			UpdateAttemptsLabel();
		}
		public void AttemptsSubClick(object sender, RoutedEventArgs args) {
			if (userData.Attempts > 0) {
				userData.Attempts--;
			}
			UpdateAttemptsLabel();
		}
		public void CompletionsAddClick(object sender, RoutedEventArgs args) {
			userData.Completions++;
			UpdateCompletionsLabel();
		}
		public void CompletionsSubClick(object sender, RoutedEventArgs args) {
			if (userData.Completions > 0) {
				userData.Completions--;
			}
			UpdateCompletionsLabel();
		}

		public void UpdateAttemptsLabel() {
			attemptsLabel.Content = "Attempts: " + userData.Attempts.ToString();
		}

		public void UpdateCompletionsLabel() {
			completionsLabel.Content = "Completions: " + userData.Completions.ToString();
		}

		public void UpdateRedemptionsLabel() {
			redemptionsLabel.Content = "Redemptions: " + userData.Redemptions.ToString();
		}


		public void CreateLiveLabel() {
			label = new Label() {
				Width = 295,
				Height = 85,
				Background = Utilities.Colors.LightGray,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(0, 0, 0, 5),
			};

			var mainStack = new StackPanel() {
				Width = 295,
			};

			var topRow = new StackPanel() {
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 2, 0, 2),
			};

			positionLabel = new Label() {
				Width = 30,
				FontFamily = Utilities.Fonts.CourierNew,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
				VerticalContentAlignment = VerticalAlignment.Center,
			};

			usernameLabel = new Label() {
				Width = 183,
				VerticalContentAlignment = VerticalAlignment.Center,
				FontFamily = Utilities.Fonts.CourierNew,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};

			redemptionTimeLabel = new Label() {
				Width = 74,
				FontFamily = Utilities.Fonts.Ariel,
				VerticalContentAlignment = VerticalAlignment.Center,
				FlowDirection = FlowDirection.RightToLeft,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};

			var secondRow = new StackPanel() {
				HorizontalAlignment = HorizontalAlignment.Right,
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 2, 0, 2),
			};

			liveTimeLabel = new Label() {
				VerticalContentAlignment = VerticalAlignment.Center,
				FontFamily = Utilities.Fonts.Ariel,
				Width = 74,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 2, 0),
				FlowDirection = FlowDirection.RightToLeft,
			};

			var middleRow = new StackPanel() {
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 2, 0, 2),
			};

			attemptsLabel = new Label() {
				Width = 90,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};

			attemptsAddButton = new Button() {
				Width = 20,
				Background = Utilities.Colors.LightGreen,
				Content = "+",
				FontFamily = Utilities.Fonts.CourierNew,
			};

			attemptsSubButton = new Button() {
				Width = 20,
				Background = Utilities.Colors.LightRed,
				Content = "-",
				FontFamily = Utilities.Fonts.CourierNew,
			};

			completionsLabel = new Label() {
				Width = 110,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};


			completionsAddButton = new Button() {
				Width = 20,
				Background = Utilities.Colors.LightGreen,
				Content = "+",
				FontFamily = Utilities.Fonts.CourierNew,
			};

			completionsSubButton = new Button() {
				Width = 20,
				Background = Utilities.Colors.LightRed,
				Content = "-",
				FontFamily = Utilities.Fonts.CourierNew,
			};

			var bottomRow = new StackPanel() {
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 2, 0, 2),
			};

			redemptionsLabel = new Label() {
				Width = 110,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};

			deleteSlotButton = new Button() {
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Margin = new Thickness(50, 0, 0, 0),
				Background = Utilities.Colors.DarkRed,
				Padding = new Thickness(5, 0, 5, 0),
				Foreground = Utilities.Colors.Black,
				BorderBrush = Utilities.Colors.Brick,
				Content = "Delete Slot",
			};

			label.MouseMove += (object sender, MouseEventArgs e) => {
				if (e.LeftButton == MouseButtonState.Pressed && MainWindow.dragging == null) {
					MainWindow.dragging = this;
					DragDrop.DoDragDrop(label, this, DragDropEffects.Move);
				} else if (MainWindow.dragging == this) {
					MainWindow.dragging = null;
				}
			};

			topRow.Children.Add(positionLabel);
			topRow.Children.Add(usernameLabel);
			topRow.Children.Add(redemptionTimeLabel);

			secondRow.Children.Add(liveTimeLabel);

			middleRow.Children.Add(attemptsLabel);
			middleRow.Children.Add(attemptsAddButton);
			middleRow.Children.Add(attemptsSubButton);
			middleRow.Children.Add(completionsLabel);
			middleRow.Children.Add(completionsAddButton);
			middleRow.Children.Add(completionsSubButton);

			bottomRow.Children.Add(redemptionsLabel);
			bottomRow.Children.Add(deleteSlotButton);

			mainStack.Children.Add(topRow);
			mainStack.Children.Add(secondRow);
			mainStack.Children.Add(middleRow);
			mainStack.Children.Add(bottomRow);

			label.Content = mainStack;
		}
	}

	public class QueueSlot : DraggableSlot {
		//public Label label;
		public Label positionLabel;
		public Label usernameLabel;
		public Label redemptionTimeLabel;
		public Label attemptsLabel;
		public Label completionsLabel;
		public Label redemptionsLabel;
		public Button attemptsAddButton;
		public Button attemptsSubButton;
		public Button completionsAddButton;
		public Button completionsSubButton;
		public Button deleteSlotButton;

		private bool alive;
		public long redemptionTime;
		public UserData userData;

		private Thread thread;

		public static List<QueueSlot> Queue = new List<QueueSlot>();

		private static void UpdateSlotNumbers() {
			for (int i = 0; i < Queue.Count; i++) {
				Queue[i].positionLabel.Content = "#" + (i + 1).ToString();
			}
		}

		public static void Move(QueueSlot slot, int pos) {
			Queue.Remove(slot);
			if (pos >= Queue.Count) {
				Queue.Add(slot);
			} else {
				Queue.Insert(pos, slot);
			}
			UpdateSlotNumbers();
			RefreshQueueStack();
		}

		public static void RefreshQueueStack() {
			try {
				var children = MainWindow.Reference.QueueStack.Children;
				if (Queue.Count == 0) {
					children.Clear();
					return;
				}
				for (int i = 0; i < Queue.Count; i++) {
					if (children.Count <= i) {
						children.Add(Queue[i].label);
					} else if (Queue[i].label != children[i]) {
						children.Insert(i, Queue[i].label);
					}
				}
				if (children.Count > Queue.Count) {
					children.RemoveRange(Queue.Count, children.Count - Queue.Count);
				}
			} catch {
				var children = MainWindow.Reference.QueueStack.Children;
				children.Clear();
				for (int i = 0; i < Queue.Count; i++) {
					children.Add(Queue[i].label);
				}
			}
		}

		public static void AddSlot(QueueSlot slot) {
			Queue.Add(slot);
			RefreshQueueStack();
			UpdateSlotNumbers();
		}

		public static void AddSlot(QueueSlot slot, int pos) {
			if (pos >= Queue.Count) {
				Queue.Add(slot);
			} else {
				Queue.Insert(pos, slot);
			}
			RefreshQueueStack();
			UpdateSlotNumbers();
		}

		public static void RemoveSlot(QueueSlot slot) {
			var at = Queue.FindIndex(a => a == slot);
			if (at < 0) {
				return;
			}
			Queue.RemoveAt(at);
			try {
				if (MainWindow.Reference.QueueStack.Children[at] == slot.label) {
					MainWindow.Reference.QueueStack.Children.RemoveAt(at);
				} else {
					RefreshQueueStack();
				}
			} catch {
				RefreshQueueStack();
			}
			UpdateSlotNumbers();
		}

		public static QueueSlot FromLive(LiveSlot slot) {
			return FromLive(slot, Queue.Count);
		}

		public static QueueSlot FromLive(LiveSlot slot, int pos) {
			LiveSlot.RemoveSlot(slot);
			var ret = new QueueSlot();
			ret.isQueue = true;

			MainWindow.Reference.QueueStack.Dispatcher.Invoke(delegate {
				ret.CreateQueueLabel();
				ret.usernameLabel.Content = slot.userData.UserName;
				ret.redemptionTime = slot.redemptionTime;
				ret.userData = slot.userData;
				ret.UpdateAttemptsLabel();
				ret.UpdateCompletionsLabel();
				ret.UpdateRedemptionsLabel();

				ret.attemptsAddButton.Click += ret.AttemptsAddClick;
				ret.attemptsSubButton.Click += ret.AttemptsSubClick;
				ret.completionsAddButton.Click += ret.CompletionsAddClick;
				ret.completionsSubButton.Click += ret.CompletionsSubClick;

				ret.deleteSlotButton.Click += ret.Cleanup;
				ret.alive = true;

				ret.thread = new Thread(ret.Updater);
				ret.thread.Start();

				AddSlot(ret, pos);
			});
			return ret;
		}

		private QueueSlot() { }

		public QueueSlot(UserData userData, long redemptionTime) {
			isQueue = true;
			MainWindow.Reference.QueueStack.Dispatcher.Invoke(delegate {
				CreateQueueLabel();
				usernameLabel.Content = userData.UserName;
				this.redemptionTime = redemptionTime;
				this.userData = userData;
				UpdateAttemptsLabel();
				UpdateCompletionsLabel();
				UpdateRedemptionsLabel();

				attemptsAddButton.Click += AttemptsAddClick;
				attemptsSubButton.Click += AttemptsSubClick;
				completionsAddButton.Click += CompletionsAddClick;
				completionsSubButton.Click += CompletionsSubClick;

				deleteSlotButton.Click += Cleanup;
				alive = true;

				thread = new Thread(Updater);
				thread.Start();

				AddSlot(this);
			});
		}

		public void Updater() {
			redemptionTimeLabel.Dispatcher.Invoke(delegate {
				try {
					redemptionTimeLabel.Content = Utilities.Times.FormatTime(Utilities.Times.GetTimeSpent(redemptionTime) + 50, false);
				} catch { }
			});
			while (alive) {
				Thread.Sleep(1000 - (int)(Utilities.Times.GetTimeSpent(redemptionTime) % 1000));
				if (alive) {
					try {
						redemptionTimeLabel.Dispatcher.Invoke(delegate {
							try {
								redemptionTimeLabel.Content = Utilities.Times.FormatTime(Utilities.Times.GetTimeSpent(redemptionTime) + 50, false);
							} catch { }
						});
					} catch { }
				}
				Thread.Sleep(50);
			}
		}

		public void Cleanup() {
			alive = false;
			try {
				thread.Abort();
			} catch { }
			RemoveSlot(this);
		}

		public void Cleanup(object sender, RoutedEventArgs args) {
			Cleanup();
		}

		public void AttemptsAddClick(object sender, RoutedEventArgs args) {
			userData.Attempts++;
			UpdateAttemptsLabel();
		}
		public void AttemptsSubClick(object sender, RoutedEventArgs args) {
			if (userData.Attempts > 0) {
				userData.Attempts--;
			}
			UpdateAttemptsLabel();
		}
		public void CompletionsAddClick(object sender, RoutedEventArgs args) {
			userData.Completions++;
			UpdateCompletionsLabel();
		}
		public void CompletionsSubClick(object sender, RoutedEventArgs args) {
			if (userData.Completions > 0) {
				userData.Completions--;
			}
			UpdateCompletionsLabel();
		}

		public void UpdateAttemptsLabel() {
			attemptsLabel.Content = "Attempts: " + userData.Attempts.ToString();
		}

		public void UpdateCompletionsLabel() {
			completionsLabel.Content = "Completions: " + userData.Completions.ToString();
		}

		public void UpdateRedemptionsLabel() {
			redemptionsLabel.Content = "Redemptions: " + userData.Redemptions.ToString();
		}


		public void CreateQueueLabel() {
			label = new Label() {
				Width = 295,
				Height = 65,
				Background = Utilities.Colors.LightGray,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(0, 0, 0, 5),
			};

			var mainStack = new StackPanel() {
				Width = 295,
			};

			var topRow = new StackPanel() {
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 2, 0, 2),
			};

			positionLabel = new Label() {
				Width = 30,
				FontFamily = Utilities.Fonts.CourierNew,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
				VerticalContentAlignment = VerticalAlignment.Center,
			};

			usernameLabel = new Label() {
				Width = 183,
				VerticalContentAlignment = VerticalAlignment.Center,
				FontFamily = Utilities.Fonts.CourierNew,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};

			redemptionTimeLabel = new Label() {
				Width = 74,
				FontFamily = Utilities.Fonts.Ariel,
				VerticalContentAlignment = VerticalAlignment.Center,
				//HorizontalContentAlignment = HorizontalAlignment.Right,
				FlowDirection = FlowDirection.RightToLeft,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};

			var middleRow = new StackPanel() {
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 2, 0, 2),
			};

			attemptsLabel = new Label() {
				Width = 90,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};

			attemptsAddButton = new Button() {
				Width = 20,
				Background = Utilities.Colors.LightGreen,
				Content = "+",
				FontFamily = Utilities.Fonts.CourierNew,
			};

			attemptsSubButton = new Button() {
				Width = 20,
				Background = Utilities.Colors.LightRed,
				Content = "-",
				FontFamily = Utilities.Fonts.CourierNew,
			};

			completionsLabel = new Label() {
				Width = 110,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};


			completionsAddButton = new Button() {
				Width = 20,
				Background = Utilities.Colors.LightGreen,
				Content = "+",
				FontFamily = Utilities.Fonts.CourierNew,
			};

			completionsSubButton = new Button() {
				Width = 20,
				Background = Utilities.Colors.LightRed,
				Content = "-",
				FontFamily = Utilities.Fonts.CourierNew,
			};

			var bottomRow = new StackPanel() {
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 2, 0, 2),
			};

			redemptionsLabel = new Label() {
				Width = 110,
				Padding = Utilities.ZeroThickness,
				Margin = new Thickness(2, 0, 0, 0),
			};

			deleteSlotButton = new Button() {
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Margin = new Thickness(50, 0, 0, 0),
				Background = Utilities.Colors.DarkRed,
				Padding = new Thickness(5, 0, 5, 0),
				Foreground = Utilities.Colors.Black,
				BorderBrush = Utilities.Colors.Brick,
				Content = "Delete Slot",
			};

			label.MouseMove += (object sender, MouseEventArgs e) => {
				if (e.LeftButton == MouseButtonState.Pressed && MainWindow.dragging == null) {
					MainWindow.dragging = this;
					DragDrop.DoDragDrop(label, this, DragDropEffects.Move);
				} else if (MainWindow.dragging == this) {
					MainWindow.dragging = null;
				}
			};

			topRow.Children.Add(positionLabel);
			topRow.Children.Add(usernameLabel);
			topRow.Children.Add(redemptionTimeLabel);

			middleRow.Children.Add(attemptsLabel);
			middleRow.Children.Add(attemptsAddButton);
			middleRow.Children.Add(attemptsSubButton);
			middleRow.Children.Add(completionsLabel);
			middleRow.Children.Add(completionsAddButton);
			middleRow.Children.Add(completionsSubButton);

			bottomRow.Children.Add(redemptionsLabel);
			bottomRow.Children.Add(deleteSlotButton);

			mainStack.Children.Add(topRow);
			mainStack.Children.Add(middleRow);
			mainStack.Children.Add(bottomRow);

			label.Content = mainStack;
		}
	}

	public static class Utilities {

		public static readonly Thickness ZeroThickness = new Thickness();

		public static class Fonts {
			public static readonly FontFamily Ariel = new FontFamily("Ariel");
			public static readonly FontFamily CourierNew = new FontFamily("Courier New");
			public static readonly FontFamily DefaultFont = new FontFamily("Courier New");
		}

		public static class Times {
			public static long GetCurrentMillis() {
				return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
			}

			public static long GetTimeSpent(long startingMillis) {
				return GetCurrentMillis() - startingMillis;
			}

			public static string FormatTime(long span, bool includeMillis) {
				var str = "";
				var ts = TimeSpan.FromMilliseconds(span);
				if (ts.Hours > 0) {
					str = ts.Hours.ToString() + "h " + ts.Minutes.ToString() + "m " + ts.Seconds.ToString() + "s";
				}else if (ts.Minutes > 0) {
					str = ts.Minutes.ToString() + "m " + ts.Seconds.ToString() + "s";
				}else {
					str = ts.Seconds.ToString() + "s";
				}
				return str;
			}
		}

		public static class Colors {
			public static readonly Brush LightGray = new SolidColorBrush(Color.FromRgb(221, 221, 221));
			public static readonly Brush LightGreen = new SolidColorBrush(Color.FromRgb(154, 245, 168));
			public static readonly Brush LightRed = new SolidColorBrush(Color.FromRgb(255, 160, 160));
			public static readonly Brush DarkRed = new SolidColorBrush(Color.FromRgb(255, 89, 89));
			public static readonly Brush Black = new SolidColorBrush(Color.FromRgb(0, 0, 0));
			public static readonly Brush Brick = new SolidColorBrush(Color.FromRgb(127, 54, 54));
		}

		public static class Labels {
			public const VerticalAlignment DefaultVerticalAlignment = VerticalAlignment.Stretch;
			public const VerticalAlignment DefaultVerticalContentAlignment = VerticalAlignment.Top;
			public const HorizontalAlignment DefaultHorizontalAlignment = HorizontalAlignment.Stretch;
			public const HorizontalAlignment DefaultHorizontalContentAlignment = HorizontalAlignment.Left;
			public const FlowDirection DefaultFlowDirection = FlowDirection.LeftToRight;
			public static readonly FontFamily DefaultFontFamily = new FontFamily("Courier New");
			public static readonly double DefaultFontSize = 12;
			public static readonly Thickness DefaultMargin = new Thickness();
			public static readonly Thickness DefaultPadding = new Thickness();
		}
	}

	public partial class MainWindow : Window {
		public static DraggableSlot dragging;

		private HttpClient httpClient = new HttpClient();
		private readonly string ClientId = "Insert Client Id Here";
		private readonly string CaperId = "486192432";
		public readonly string CaperName = "incoxicated";

		public static MainWindow Reference;
		public string path;
		public SaveDataClass CurrentLoad;
		public TwitchClient twitchClient;
		public TwitchAPI apiClient;
		public TwitchPubSub pubSub;
		public WebSocketClient sock;

		private bool ShouldBeConnected = false;
		private int pingerCount = 0;

		public MainWindow() {
			InitializeComponent();

			Reference = this;
			Thread.CurrentThread.IsBackground = true;

			path = Assembly.GetExecutingAssembly().Location;
			string[] split = path.Split('\\');
			path = "";
			for (int i = 0; i < split.Length - 2; i++) {
				path += split[i] + "\\";
			}

			if (File.Exists(path + "Main.json")) {
				try {
					CurrentLoad = JsonSerializer.Deserialize<SaveDataClass>(File.ReadAllText(path + "Main.json"));
				} catch {
					CurrentLoad = new SaveDataClass() {
						OAuth = "",
						AutoConnect = false,
						SaveUserData = new List<UserData>(),
					};
				}
			} else {
				CurrentLoad = new SaveDataClass() {
					OAuth = "",
					AutoConnect = false,
					SaveUserData = new List<UserData>(),
				};
			}

			foreach (UserData ud in CurrentLoad.SaveUserData) {
				SaveDataClass.UserDataDic.Add(ud.Id, ud);
			}

			new Thread(running).Start();
			new Thread(AutoSave).Start();
		}

		public void running() {
			Thread.Sleep(200);

			httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + CurrentLoad.OAuth);
			httpClient.DefaultRequestHeaders.Add("Client-Id", ClientId);

			OAuthUrlButton.Click += delegate {
				OpenUrl("https://id.twitch.tv/oauth2/authorize?response_type=token&client_id=" + ClientId + "&redirect_uri=https://twitchapps.com/tokengen/&scope=channel:read:redemptions+chat:read+chat:edit");
			};
			SaveOAuthButton.Click += delegate {
				if (CurrentLoad != null) {
					CurrentLoad.OAuth = OAuthBox.Password;
				}
			};
			ConnectButton.Click += delegate {
				new Thread(ConnectClient).Start();
			};
			DisconnectButton.Click += delegate {
				DisconnectClient();
				SaveMain();
			};
			QueueRedemptionNameBox.TextChanged += delegate {
				if (CurrentLoad != null) {
					CurrentLoad.QueueRedeemName = QueueRedemptionNameBox.Text;
				}
			};
			Closing += onClose;
			QueueScrollViewer.Drop += (object sender, DragEventArgs e) => {
				if (dragging != null) {
					if (dragging.isQueue) {
						QueueSlot.Move((QueueSlot)dragging, (int)Math.Floor((e.GetPosition(QueueScrollViewer).Y + QueueScrollViewer.ContentVerticalOffset) / 70));
					} else {
						QueueSlot.FromLive((LiveSlot)dragging);
					}
				}
				dragging = null;
			};

			LiveDragger.Drop += (object sender, DragEventArgs e) => {
				if (dragging != null) {
					if (!dragging.isQueue) {
						LiveSlot.Move((LiveSlot)dragging, (int)Math.Floor(e.GetPosition(LiveStack).Y / 95));
					} else {
						LiveSlot.FromQueue((QueueSlot)dragging, (int)Math.Floor(e.GetPosition(LiveStack).Y / 95));
					}
				} else {
				}
				dragging = null;
			};

			AutoConnectToggle.Click += delegate {
				CurrentLoad.AutoConnect = AutoConnectToggle.IsChecked == true;
				if (CurrentLoad.AutoConnect) {
					if (twitchClient == null && pubSub == null && !ShouldBeConnected) {
						new Thread(ConnectClient).Start();
					}
				}
			};

			QueueCommandToggleButton.Click += delegate {
				CurrentLoad.QueueCommandToggle = QueueCommandToggleButton.IsChecked == true;
				try {
					SaveMain();
				} catch { }
			};

			PartyCommandToggleButton.Click += delegate {
				CurrentLoad.PartyCommandToggle = PartyCommandToggleButton.IsChecked == true;
				try {
					SaveMain();
				} catch { }
			};

			StatsCommandToggleButton.Click += delegate {
				CurrentLoad.StatsCommandToggle = StatsCommandToggleButton.IsChecked == true;
				try {
					SaveMain();
				} catch { }
			};

			AutoConnectToggle.Dispatcher.BeginInvoke(delegate {
				AutoConnectToggle.IsChecked = CurrentLoad.AutoConnect;
				OAuthBox.Password = CurrentLoad.OAuth != null ? CurrentLoad.OAuth : "";
				QueueRedemptionNameBox.Text = CurrentLoad.QueueRedeemName != null ? CurrentLoad.QueueRedeemName : "";
				QueueCommandToggleButton.IsChecked = CurrentLoad.QueueCommandToggle;
				PartyCommandToggleButton.IsChecked = CurrentLoad.PartyCommandToggle;
				StatsCommandToggleButton.IsChecked = CurrentLoad.StatsCommandToggle;
				try {
					SaveMain();
				} catch { }
			});

			if (CurrentLoad.AutoConnect) {
				new Thread(ConnectClient).Start();
			}
		}

		public void AutoSave() {
			while (true) {
				Thread.Sleep(TimeSpan.FromSeconds(60));
				SaveMain();
			}
		}

		private long lastSave = 0;
		public void SaveMain() {
			if (DateTime.Now.Ticks - lastSave < TimeSpan.FromSeconds(2).Ticks) return;
			lastSave = DateTime.Now.Ticks;
			File.Delete(path + "Main.json");
			FileStream file = new FileStream(path + "Main.json", FileMode.OpenOrCreate, FileAccess.ReadWrite);
			file.Write(Encoding.ASCII.GetBytes(JsonSerializer.Serialize(CurrentLoad)));
			file.Flush();
			file.Close();
		}

		private void pinger() {
			int id = pingerCount;
			Thread.Sleep(TimeSpan.FromSeconds(15));
			while (pingerCount == id) {
				Thread.Sleep(TimeSpan.FromSeconds(15));
				if (pingerCount != id) return;
				twitchClient.SendRaw("PONG irc.twitch.tv");
				sock.Send(new JObject(new JProperty("type", "PING")).ToString());
			}
		}

		public void ConnectClient() {
			if (CurrentLoad.OAuth == null || CurrentLoad.OAuth.Length < 5 || ShouldBeConnected) {
				TestLogging("Invalid OAuth or already connected!");
				return;
			};

			ShouldBeConnected = true;
			pingerCount++;

			try {
				if (pubSub != null) {
					pubSub.Disconnect();
				}
			} catch { }
			pubSub = null;

			if (apiClient != null) {
				apiClient = null;
			}

			try {
				if (twitchClient != null) {
					twitchClient.Disconnect();
				}
			} catch { }
			twitchClient = null;

			pubSub = new TwitchPubSub();
			apiClient = new TwitchAPI();
			twitchClient = new TwitchClient();
			twitchClient.Initialize(new TwitchLib.Client.Models.ConnectionCredentials(CaperName, CurrentLoad.OAuth), CaperName);

			twitchClient.OnChatCommandReceived += onClientCommand;

			apiClient.Settings.ClientId = ClientId;
			apiClient.Settings.AccessToken = CurrentLoad.OAuth;

			FieldInfo type = typeof(TwitchPubSub).GetField("_socket", BindingFlags.NonPublic | BindingFlags.Instance);
			sock = (WebSocketClient)type.GetValue(pubSub);

			new Thread(pinger).Start();

			pubSub.OnPubSubServiceConnected += onPubSubServiceConnected;
			pubSub.OnPubSubServiceClosed += onPubSubServiceDisconnected;
			pubSub.OnListenResponse += onListenResponse;
			pubSub.OnChannelPointsRewardRedeemed += redeem;

			twitchClient.OnDisconnected += delegate (object sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs args) {
				if (ShouldBeConnected) {
					if (twitchClient != null) {
						if (!twitchClient.IsInitialized) {
							twitchClient.Initialize(new TwitchLib.Client.Models.ConnectionCredentials(CaperName, CurrentLoad.OAuth), CaperName);
						}
						twitchClient.Connect();
					}
				}
			};

			try {
				SaveMain();
			} catch { }

			try {
				pubSub.ListenToChannelPoints(CaperId);
			} catch {
				DisconnectClient();
				TestLogging("PubSub broke! try updating the oauth or restarting the bot.");
				return;
			}

			try {
				pubSub.Connect();
			} catch {
				DisconnectClient();
				TestLogging("PubSub broke! try updating the oauth or restarting the bot.");
				return;
			}

			try {
				twitchClient.Connect();
			} catch {
				DisconnectClient();
				TestLogging("TwitchClient broke! try updating the oauth or restarting the bot.");
				return;
			}
		}
		private void onPubSubServiceConnected(object sender, EventArgs e) {

			TestLogging("pub connect");
			if (pubSub == null) {
				return;
			}
			pubSub.SendTopics(CurrentLoad.OAuth);
		}

		private void onPubSubServiceDisconnected(object sender, EventArgs e) {
			if (ShouldBeConnected && pubSub != null) {
				pubSub.Connect();
			}
		}

		private void onListenResponse(object sender, OnListenResponseArgs e) {
			if (!e.Successful) {
				try {
					DisconnectClient();
				} catch { }
			}

			TestLogging("listened "+ e.Response.Error);
		}

		private long lastQueueCommand = 0;
		private long lastPartyCommand = 0;
		private long lastStatsCommand = 0;
		private void onClientCommand(object sender, OnChatCommandReceivedArgs e) {
			if (e.Command.CommandText.ToLower() == "queue" && CurrentLoad.QueueCommandToggle) {
				if (Utilities.Times.GetTimeSpent(lastQueueCommand) < 5000) {
					return;
				}
				lastQueueCommand = Utilities.Times.GetCurrentMillis();
				for (int i = 0; i < QueueSlot.Queue.Count; i++) {
					if (QueueSlot.Queue[i].userData.Id == e.Command.ChatMessage.UserId) {
						twitchClient.SendMessage("@" + e.Command.ChatMessage.Username + " You are in position " + (i + 1).ToString() + " out of " + QueueSlot.Queue.Count.ToString());
						return;
					}
				}
				twitchClient.SendMessage("@" + e.Command.ChatMessage.Username + " You are not in the queue!");
				return;
			} else if (e.Command.CommandText.ToLower() == "party" && CurrentLoad.PartyCommandToggle) {
				if (Utilities.Times.GetTimeSpent(lastPartyCommand) < 10000) {
					return;
				}
				lastPartyCommand = Utilities.Times.GetCurrentMillis();
				if (LiveSlot.Live.Count == 0) {
					twitchClient.SendMessage("@" + e.Command.ChatMessage.Username + " No one is currently in the party!");
					return;
				}
				string str = "";
				string goal = "";
				for (int i = 0; i < LiveSlot.Live.Count; i++) {
					goal = "";
					if (i > 0) {
						if (LiveSlot.Live.Count == 2) {
							goal = " and ";
						} else if (i == LiveSlot.Live.Count - 1) {
							goal = ", and ";
						} else {
							goal = ", ";
						}
					}
					goal += LiveSlot.Live[i].userData.UserName;
					str += goal;
				}
				twitchClient.SendMessage("@" + e.Command.ChatMessage.Username + " The current part is: " + str + ".");
				return;
			} else if (e.Command.CommandText.ToLower() == "stats" && CurrentLoad.StatsCommandToggle) {
				if (Utilities.Times.GetTimeSpent(lastStatsCommand) < 5000) {
					return;
				}
				lastStatsCommand = Utilities.Times.GetCurrentMillis();
				if (SaveDataClass.UserDataDic.ContainsKey(e.Command.ChatMessage.UserId)) {
					var ud = SaveDataClass.UserDataDic[e.Command.ChatMessage.UserId];
					twitchClient.SendMessage("@" + e.Command.ChatMessage.Username + " You have attempted " + ud.Attempts.ToString() + " times and have completed " + ud.Completions.ToString() + " times!");
					return;
				}
				twitchClient.SendMessage("@" + e.Command.ChatMessage.Username + " You don't have any stats!");
				return;
			}
		}

		public void DisconnectClient() {
			ShouldBeConnected = false;
			pingerCount++;

			//client = null;

			if (twitchClient != null) {
				try {
					twitchClient.Disconnect();
				} catch {
					CurrentLoad.AutoConnect = false;
					AutoConnectToggle.Dispatcher.Invoke(delegate {
						AutoConnectToggle.IsChecked = false;
					});
				}
			}
			twitchClient = null;

			if (pubSub != null) {
				try {
					pubSub.Disconnect();
				} catch {
					CurrentLoad.AutoConnect = false;
					AutoConnectToggle.Dispatcher.Invoke(delegate {
						AutoConnectToggle.IsChecked = false;
					});
				}
			}
			pubSub = null;

			TestLogging("Disconnected!");
		}

		public void TestLogging(object str) {
			LogOutput.Dispatcher.Invoke(delegate {
				LogOutput.Text = str.ToString();
			});
		}

		public void onClose(object sender, EventArgs e) {
			try {
				SaveMain();
			} catch { };
			try {
				DisconnectClient();
			} catch { };
		}

		private void OpenUrl(string url) {
			try {
				Process.Start(url);
			} catch {
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
					url = url.Replace("&", "^&");
					Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
				} else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
					Process.Start("xdg-open", url);
				} else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
					Process.Start("open", url);
				} else {
					throw;
				}
			}
		}

		public bool IsInQueue(UserData userData) {
			foreach (QueueSlot qs in QueueSlot.Queue) {
				if (qs.userData.Id == userData.Id) {
					return true;
				}
			}
			return false;
		}

		public class twitchUserInfo {
			public string id { get; set; }
			public string login { get; set; }
			public string display_name { get; set; }
		}

		public class twitchUsers {
			public List<twitchUserInfo> data { get; set; }
		}

		private void Redeem(object sender, OnChannelPointsRewardRedeemedArgs e) {
			if (!ShouldBeConnected) return;
			string title = e.RewardRedeemed.Redemption.Reward.Title.TrimEnd();
			if (title.ToLower().Equals(CurrentLoad.QueueRedeemName.ToLower())) {
				UserData ud;
				if (SaveDataClass.UserDataDic.ContainsKey(e.RewardRedeemed.Redemption.User.Id)){
					ud = SaveDataClass.UserDataDic[e.RewardRedeemed.Redemption.User.Id];
				} else {
					ud = new UserData() {
						Id = e.RewardRedeemed.Redemption.User.Id,
						Redemptions = 0,
						Attempts = 0,
						Completions = 0,
					};
					CurrentLoad.AddUserData(ud);
				}
				ud.UserName = e.RewardRedeemed.Redemption.User.DisplayName;

				if (!IsInQueue(ud)) {
					ud.Redemptions++;
					new QueueSlot(ud, Utilities.Times.GetCurrentMillis());
				}
			}
		}

		private void redeem(object sender, OnChannelPointsRewardRedeemedArgs e) {
			if (!ShouldBeConnected) return;
			Dispatcher.BeginInvoke(delegate {
				Redeem(sender, e);
			});
		}
	}

	public class UserData {
		public string UserName { get; set; }
		public string Id { get; set; }
		public int Redemptions { get; set; }
		public int Attempts { get; set; }
		public int Completions { get; set; }
	}

	public class SaveDataClass {
		public string OAuth { get; set; }
		public bool AutoConnect { get; set; }
		public string QueueRedeemName { get; set; }
		public bool QueueCommandToggle { get; set; }
		public bool PartyCommandToggle { get; set; }
		public bool StatsCommandToggle { get; set; }
		public List<UserData> SaveUserData { get; set; }

		public static Dictionary<string, UserData> UserDataDic = new Dictionary<string, UserData>();

		public void AddUserData(UserData ud) {
			SaveUserData.Add(ud);
			UserDataDic.Add(ud.Id, ud);
		}
	}

	public static class Extensions {
		public static void SendMessage(this TwitchClient client, string message) {
			MainWindow.Reference.TestLogging(message);
			client.SendMessage(MainWindow.Reference.CaperName, message);
		}
	}
}