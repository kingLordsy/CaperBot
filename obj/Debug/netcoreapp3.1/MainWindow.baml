   M S B A M L   `   `   `  ����C  ?CaperBot, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null#clr-namespace:CaperBotCaperBot  5   	       CaperBot.MainWindow   5   	   W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   (localclr-namespace:CaperBot     5   	     � Title$  
MainWindow��5   	   $	��450��6   $
��1000��6)   ���	#FF1A1A1A.��5	      � $
��Left=�6   $	��Top=�6#   $	��980��6;   $	��400��6G   .+�5
      �� $	��310��6
   $	��Top=�6   $
��Left=�6.   $��Center=�6I   $��10,0,0,0q�6m   ���	#FFB1B1B1$��0��6�   $��0��6�   .��6�   Queue5      �� 5      -   5        $��QueueScrollViewer��5     $
��Left=�6   $	��375��63   $	��325��6@   $
��Left=�6L   $��Center=�6g   $��	10,30,0,0q�6�   $��Disabled=�6�   $��Visible=�6�   ���	#FF272727$��0��6�    J� 	AllowDrop$
 True��6$  .��5      �� -   5        $��
QueueStack��5      $
��Left=�6#   $	��Top=�6>    �� Orientation$ Vertical=�6V   $��5,5,5,5q�6m   5      5      �� 5      $	��305��6
   $	��Top=�6   $
��Left=�6.   $��Center=�6I   $��	345,0,0,0q�6m   ���	#FFB1B1B1$��0��6�   $��0��6�   .��6�   Live5      � 5      -   5        $��LiveDragger��5   �   ���	#FF272727$	��305��6    $	��375��6,   $��
345,30,0,0q�69   $��0��6M   $
��Left=�6]   $��Center=�6x   $
 true��6�   .+�5      �� -   5        $��	LiveStack��5      $	��295��6!   $	��365��6-   $
��Left=�6:   $	��Top=�6U   $ Vertical=�6m   $��5,5,5,5q�6�   $
 True��6�   5      5      � 5      ���	#FF272727$	��305��6    $	��375��6,   $��
665,30,0,0q�69   $��0��6M   $
��Left=�6]   $��Center=�6x   $��2��6�   .+�5      �� .+�5      �� $ 
Horizontal=�6   .+�5      �� ���	#FFB1B1AA$	��115��6$   $��5q�60   $��0,0,0,0q�6;   .��6M   Redemption Name:5      �� 5       -   5        $��QueueRedemptionNameBox��5    &   $	��170��6   $��5q�6   .��6D   5"      5#      �� 5%      $ 
Horizontal=�6   .+�5&      �� -   5        $��QueueCommandToggleButton��5&   �   $
��Left=�6   $��5q�6+   $	��Top=�66   ���	#FFB1B1B1$��RightToLeft=�6e   .��6�   :Queue Command Toggle5(      5)      �� 5*      $ 
Horizontal=�6   .+�5+      �� -   5        $��PartyCommandToggleButton��5+   �   $
��Left=�6   $��5q�6+   $	��Top=�66   ���	#FFB1B1B1$��RightToLeft=�6e   .��6�   :Party Command Toggle5-      5.      �� 5/      $ 
Horizontal=�6   .+�50      �� -   5        $��StatsCommandToggleButton��50   �   $
��Left=�6   $��5q�6+   $	��Top=�66   ���	#FFB1B1B1$��RightToLeft=�6e   .��6�   :Stats Command Toggle52      53      �� 54      $ 
Horizontal=�6   .+�55      �� ���	#FFB1B1AA$��40��6$   $��5q�6/   $��0,0,0,0q�6:   .��6L   OAuth:57      G� 58      -	   5        $��OAuthBox��58   *   $	��100��6   $��5q�6   5:      �� 5;      -
   5        $��SaveOAuthButton��5;   2   $��65,0,0,0q�6   $��5,1,5,1q�6    .��6I   
Save OAuth5=      5>      �� 5?      $ 
Horizontal=�6   .+�5@      �� -   5        $��ConnectButton��5@   2   $��30,0,0,0q�6   $��5,4,5,4q�6    .��6G   	Connect5B      �� 5C      -   5        $��DisconnectButton��5C   2   $��30,0,0,0q�6   $��5,4,5,4q�6    .��6J   
Disconnect5E      �� 5F      -   5        $��AutoConnectToggle��5F   T   $��Center=�6   $��10,2,0,0q�6+   ���	#FFB1B1B1.��6m   Auto-Connect5H      5I      �� 5J      $ 
Horizontal=�6   .+�5K      �� -   5        $��	LogOutput��5K      $	��305��6"   ���	#FFB1B1B1$	��100��6F   $��WrapWithOverflow=�6S    �� TextAlignment$ Center=�6s   ��asdfasdgasdgfaslkdfhaskd fasdjfhaksljdhf askjdlh fkaljsdhf kaljshdflksaj fhkasljdhf alksjdhf askljdhf kasjdhf askjdfkajshdf kasjhdfkljashf asd5M      5N      �� 5O      $ 
Horizontal=�6   $��Center=�6*   .+�5P      �� -   5        $��OAuthUrlButton��5P   ,   $��10q�6   $��5,2,5,2q�6   .��6B   Open OAuth Url5R      5S      5T      5U      5V      5W      