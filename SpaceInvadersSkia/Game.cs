using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace SpaceInvadersSkia
{
    public class Game : SKCanvasView
    {
        public Game()
        {
            EnableTouchEvents = true;

            var ms = 1000.0 / _fps;
            var ts = TimeSpan.FromMilliseconds(ms);
            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            _dpi = mainDisplayInfo.Density;

            Device.StartTimer(ts, TimerLoop);

            _primaryPaint = new SKPaint()
            {
                TextSize = 100,
                Color = Color.LimeGreen.ToSKColor()
            };

            _secondaryPaint = new SKPaint()
            {
                TextSize = 36,
                Color = Color.White.ToSKColor()
            };
        }
        private double _dpi;

        private bool TimerLoop()
        {
            // get the elapsed time from the stopwatch because the 1/30 timer interval is not accurate and can be off by 2 ms
            var dt = _stopWatch.Elapsed.TotalSeconds;

            _stopWatch.Restart();

            // calculate current fps
            var fps = dt > 0 ? 1.0 / dt : 0;

            // when the fps is too low reduce the load by skipping the frame
            if (fps < _fps / 2)
                return true;

            _fpsCount++;

            if (_fpsCount == 20)
            {
                _fpsCount = 0;
            }

            InvalidateSurface();

            return true;
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            //TODO aliens are not attacking
            base.OnPaintSurface(e);
            
            _info = e.Info;
            var canvas = e.Surface.Canvas;

            const string Fire = "Fire";
            const string YouWin = "YOU WIN";
            const string GameOver = "GAME OVER";
            
            if (!_aliensLoaded)
                LoadAliens();

            if (_selectedCoordinate.Y == 0)
                _selectedCoordinate = new SKPoint(_info.Rect.MidX, _info.Rect.MidY);

            if (_aliens.Count == 0)
            {
                //TODO code smell
                _isGameOver = true;
                PresentEndGame(canvas, YouWin);
                return;
            }

            if (_isGameOver)
            {
                PresentEndGame(canvas, GameOver);
                return;
            }
            canvas.Clear();

            _jet = SKPath.ParseSvgPathData(Constants.JetSVG);

            // calculate the scaling need to fit to screen
            var scaleX = 100 / _jet.Bounds.Width;

            var jetMatrix = SKMatrix.CreateTranslation(
                _selectedCoordinate.X - (_jet.Bounds.Width * scaleX),
                _info.Rect.Height - _jet.Bounds.Height - _bulletDiameter);

            // draw the jet
            _jet.Transform(jetMatrix);
            canvas.DrawPath(_jet, _primaryPaint);

            //Draw fire button
            _buttonPath = new SKPath();

            var buttonCentre = new SKPoint(_info.Rect.Width - 100, _info.Rect.Height - 100);
            _buttonPath.MoveTo(buttonCentre);
            _buttonPath.LineTo(new SKPoint(buttonCentre.X, buttonCentre.Y));
            _buttonPath.ArcTo(new SKRect(
                buttonCentre.X - (_buttonDiameter / 2),
                buttonCentre.Y - (_buttonDiameter / 2),
                buttonCentre.X + (_buttonDiameter / 2),
                buttonCentre.Y + (_buttonDiameter / 2)
                ),0,350,true);

            canvas.DrawPath(_buttonPath, _primaryPaint);

            //Draw bullets
            for (int i = _bullets.Count - 1; i > -1; i--)
            {
                _bullets[i] = new SKPoint(_bullets[i].X, _bullets[i].Y - _bulletSpeed);
                canvas.DrawCircle(_bullets[i], _bulletDiameter, _primaryPaint);

                var alienTarged = _aliens.Any(alien => alien.Contains(_bullets[i].X, _bullets[i].Y));
                //Remove any aliens touched by the bullet
                _aliens.RemoveAll(alien => alien.Contains(_bullets[i].X, _bullets[i].Y));
                //Remove bullet that touched alien
                if(alienTarged)
                    _bullets.RemoveAt(i);
            }

            //Has an alien reached a horizontal edge of game?
            var switched = _aliens.Select(x => x.Bounds.Left)
                .Any(x => x < 0
                || x > _buttonPath.TightBounds.Left - (_jet.Bounds.Width / 2));

            _aliensSwarmingRight = switched ? !_aliensSwarmingRight : _aliensSwarmingRight;

            //Has an alien hit the ships y axis?
            _isGameOver = _aliens
                .Select(x => x.Bounds.Bottom)
                .Any(x => x > _jet.Bounds.Top);

            if (_isGameOver)
            {
                PresentEndGame(canvas, GameOver);
                return;
            }

            //Draw aliens
            for (var i = 0; i < _aliens.Count; i++)
            {
                //Move Aliens
                var alienMatrix = SKMatrix.CreateTranslation(
                _aliensSwarmingRight ? _alienSpeed : _alienSpeed * -1,
                switched ? 50 : 0);

                _aliens[i].Transform(alienMatrix);

                canvas.DrawPath(_aliens[i], _primaryPaint);
            }

            //Remove bullets that leave screen
            _bullets.RemoveAll(x => x.Y < 0);

            var textWidth = _secondaryPaint.MeasureText(Fire);
            canvas.DrawText(Fire, new SKPoint(_info.Rect.Width - (textWidth / 2) - 100, _info.Rect.Height - (100 - (_secondaryPaint.TextSize / 3)) ), _secondaryPaint);
        }

        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(e);

            switch (e.ActionType) {
                case SKTouchAction.Pressed:
                case SKTouchAction.Moved:
                    if (e.Location.X + (_jet.Bounds.Width / 2) < _buttonPath.TightBounds.Left)
                        _selectedCoordinate = e.Location;
                    else if (_buttonPath.Contains(e.Location.X, e.Location.Y)
                        && e.ActionType == SKTouchAction.Pressed)
                    {
                        if (_isGameOver)
                        {
                            _isGameOver = false;

                            _aliens.Clear();
                            _bullets.Clear();
                            LoadAliens();
                        }
                        else
                        {
                            Fire(true);
                        }
                    }
                    break;
            }
            e.Handled = true;
        }

        private void PresentEndGame(SKCanvas canvas, string title)
        {
            canvas.Clear();

            var textWidth = _primaryPaint.MeasureText(title);
            canvas.DrawText(title, new SKPoint(_info.Rect.MidX - (textWidth / 2), _info.Rect.MidY), _primaryPaint);

            _buttonPath = new SKPath();

            var buttonCentre = new SKPoint(_info.Rect.Width - 100, _info.Rect.Height - 100);
            _buttonPath.MoveTo(buttonCentre);
            _buttonPath.LineTo(new SKPoint(buttonCentre.X, buttonCentre.Y));
            _buttonPath.ArcTo(new SKRect(
                buttonCentre.X - (_buttonDiameter / 2),
                buttonCentre.Y - (_buttonDiameter / 2),
                buttonCentre.X + (_buttonDiameter / 2),
                buttonCentre.Y + (_buttonDiameter / 2)
                ), 0, 350, true);

            canvas.DrawPath(_buttonPath, _primaryPaint);
            var width = _secondaryPaint.MeasureText("Play");
            canvas.DrawText("Play", new SKPoint(buttonCentre.X - (width / 2), buttonCentre.Y + (_secondaryPaint.TextSize / 3)), _secondaryPaint);
        }

        private void LoadAliens()
        {
            const int AlienCount = 35;
            const int AlienSpacing = 50;
            
            for (var i = 0; i < AlienCount; i++)
            {
                var alien = SKPath.ParseSvgPathData(Constants.AlienSVG);
                var alienLength = (float)_dpi * 33;
                var alienScaleX = alienLength / alien.Bounds.Width;
                var alienScaleY = alienLength / alien.Bounds.Height;

                alien.Transform(SKMatrix.CreateScale(alienScaleX, alienScaleY));

                //how many aliens fit into legnth
                //TODO can this be moved outside the loop?
                var a = (_info.Rect.Width - _buttonDiameter) / (alien.Bounds.Width + AlienSpacing);
                var columnCount = Convert.ToInt32(a - 2);

                var columnIndex = i % columnCount;
                var rowIndex = Math.Floor(i / (double)columnCount);

                var x = alien.Bounds.Width * (columnIndex + 1) + (AlienSpacing * (columnIndex + 1));
                var y = alien.Bounds.Height * (rowIndex + 1) + (AlienSpacing * (rowIndex + 1));
                
                var alienTranslateMatrix = SKMatrix.CreateTranslation((float)x, (float)y);

                alien.Transform(alienTranslateMatrix);
                _aliens.Add(alien);
            }
            
            _aliensLoaded = true;
        }

        private void Fire(bool isPlayer, SKPoint? startingPosition = null)
        {
            if (isPlayer)
            {
                _bullets.Add(new SKPoint(_selectedCoordinate.X, _info.Rect.Height - _jet.Bounds.Height - _bulletDiameter - 20));
            }
        }

        private SKPath _jet;
        private bool _isGameOver;
        private SKImageInfo _info;
        private int _fpsCount = 0;
        private bool _aliensLoaded;
        private SKPath _buttonPath;
        private int _alienSpeed = 10;
        private int _bulletSpeed = 10;
        private const double _fps = 30;
        private int _bulletDiameter = 4;
        private bool _aliensSwarmingRight;
        private int _buttonDiameter = 100;
        private SKPoint _selectedCoordinate;
        private SKPaint _primaryPaint;
        private SKPaint _secondaryPaint;
        private List<SKPath> _aliens = new List<SKPath>();
        private List<SKPoint> _bullets = new List<SKPoint>();
        private readonly Stopwatch _stopWatch = new Stopwatch();
    }
}