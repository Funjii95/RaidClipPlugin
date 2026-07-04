using System.Reflection;
using RaidClipPlugin.Models;
using Xunit;

namespace RaidClipPlugin.Tests;

public sealed class ChatGridScrollTests
{
    [Fact]
    public void IncomingMessagesDoNotCrashWhenGridHasNoDisplayableHeight()
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MainForm();
                var grid = Assert.IsType<DataGridView>(Assert.Single(
                    form.Controls.Find("ModerationChatGrid", true)));
                grid.Visible = false;
                grid.Height = 0;
                var add = typeof(MainForm).GetMethod("AddChatMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(add);
                for (var index = 0; index < 300; index++)
                {
                    add!.Invoke(form, new object[]
                    {
                        new ChatMessage
                        {
                            Id = index.ToString(),
                            UserId = "viewer",
                            UserLogin = "viewer",
                            UserName = "Viewer",
                            Text = "Nachricht " + index,
                            ReceivedAt = DateTimeOffset.Now
                        }
                    });
                }
                Assert.Equal(250, grid.Rows.Count);
                Assert.Equal(DataGridViewAutoSizeRowsMode.None,
                    grid.AutoSizeRowsMode);
                Assert.Equal(DataGridViewTriState.False,
                    grid.DefaultCellStyle.WrapMode);
                Assert.Equal(26, grid.RowTemplate.Height);
            }
            catch (TargetInvocationException exception)
            {
                failure = exception.InnerException ?? exception;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                completed.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(completed.Wait(TimeSpan.FromSeconds(20)));
        Assert.Null(failure);
    }
}
