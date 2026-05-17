<UserControl x:Class="Musicefy.Controls.FolderLibraryControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="Transparent">
    
    <Grid>
        <ListView x:Name="FolderSongsListView" 
                  Background="Transparent" BorderThickness="0" 
                  Foreground="{DynamicResource TextBrush}"
                  ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                  MouseDoubleClick="OnSongDoubleClicked">
            <ListView.ItemContainerStyle>
                <Style TargetType="ListViewItem">
                    <Setter Property="Background" Value="Transparent"/>
                    <Setter Property="BorderThickness" Value="0"/>
                    <Setter Property="Margin" Value="0,2"/>
                    <Setter Property="Padding" Value="10,8"/>
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="ListViewItem">
                                <Border CornerRadius="8" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
                                    <ContentPresenter />
                                </Border>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                    <Style.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter Property="Background" Value="#0FFFFFFF"/>
                        </Trigger>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter Property="Background" Value="#1AFFFFFF"/>
                        </Trigger>
                    </Style.Triggers>
                </Style>
            </ListView.ItemContainerStyle>

            <ListView.View>
                <GridView>
                    <GridView.ColumnHeaderContainerStyle>
                        <Style TargetType="GridViewColumnHeader">
                            <Setter Property="Background" Value="Transparent"/>
                            <Setter Property="Foreground" Value="{DynamicResource MutedTextBrush}"/>
                            <Setter Property="BorderThickness" Value="0"/>
                            <Setter Property="HorizontalContentAlignment" Value="Left"/>
                            <Setter Property="Padding" Value="10,4"/>
                        </Style>
                    </GridView.ColumnHeaderContainerStyle>
                    
                    <GridViewColumn Header="Title" Width="280">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="🎵" Margin="0,0,10,0" Foreground="#1DB954"/>
                                    <TextBlock Text="{Binding Title}" FontWeight="Medium" Foreground="{DynamicResource TextBrush}" TextTrimming="CharacterEllipsis"/>
                                </StackPanel>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>
                    
                    <GridViewColumn Header="Artist" Width="180">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding Artist}" Foreground="{DynamicResource MutedTextBrush}" TextTrimming="CharacterEllipsis"/>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>

                    <GridViewColumn Header="Duration" Width="90">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding Duration, StringFormat=\{0:mm\\:ss\}}" Foreground="{DynamicResource MutedTextBrush}"/>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>
                </GridView>
            </ListView.View>
        </ListView>
    </Grid>
</UserControl>
