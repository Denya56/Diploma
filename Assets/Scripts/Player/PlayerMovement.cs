using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public PlayerData Data;
    #region COMPONENTS
    public Rigidbody2D _rb { get; private set; }
    public Animator _animator { get; private set; }
    public SpriteRenderer _sr { get; private set; }
    #endregion

    #region STATE PARAMETERS
    public bool IsFacingRight { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsWallJumping { get; private set; }
    public bool IsSliding { get; private set; }

    // Timers
    private float _lastOnGroundTime { get; set; }
    private float _lastOnWallTime { get; set; }
    private float _lastOnWallRightTime { get; set; }
    private float _lastOnWallLeftTime { get; set; }

    // Jump
    private bool _isJumpCut;
    private bool _isJumpFalling;

    // Wall Jump
    private float _wallJumpStartTime;
    private int _lastWallJumpDir;

    #endregion

    #region INPUT
    private Vector2 _moveInput;
    public float _lastPressedJumpTime { get; private set; }
    #endregion

    #region CHECK PARAMETERS
    [Header("Checks")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private Vector2 _groundCheckSize = new Vector2(0.5f, 0.03f);

    [SerializeField] private Transform _frontWallCheckPoint;
    [SerializeField] private Transform _backWallCheckPoint;

    [SerializeField] private Vector2 _wallCheckSize = new Vector2(0.3f, 1f);
    #endregion

    #region LAYERS & TAGS
    [Header("Layers & Tags")]
    [SerializeField] private LayerMask _groundLayer;
    #endregion

    #region PLATFORM VARIABLES
    private bool _IsOnPlatform;
    private Rigidbody2D _platformRBody2D;
    #endregion

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _sr = GetComponent<SpriteRenderer>();
    }
    void Start()
    {
        SetGravityScale(Data.gravityScale);
        IsFacingRight = true;
        GameEventSystem.Instance.OnSaveData += SaveGame;
        GameEventSystem.Instance.OnLoadData += LoadGame;
    }

    // Update is called once per frame
    void Update()
    {
        #region TIMERS
        _lastOnGroundTime -= Time.deltaTime;
        _lastOnWallTime -= Time.deltaTime;
        _lastOnWallRightTime -= Time.deltaTime;
        _lastOnWallLeftTime -= Time.deltaTime;

        _lastPressedJumpTime -= Time.deltaTime;
        #endregion

        #region INPUT HANDLER
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");

        if (_moveInput.x != 0)
            CheckDirectionToFace(_moveInput.x > 0);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnJumpInput();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            OnJumpUpInput();
        }
        #endregion

        #region COLLISION CHECKS
        if (!IsJumping)
        {
            if (Physics2D.OverlapBox(_groundCheckPoint.position, _groundCheckSize, 0, _groundLayer) && !IsJumping)
            {
                _lastOnGroundTime = Data.coyoteTime;
            }
        }

        //Right Wall Check
        if (((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && IsFacingRight)
                || (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !IsFacingRight)) && !IsWallJumping)
            _lastOnWallRightTime = Data.coyoteTime;

        //Right Wall Check
        if (((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !IsFacingRight)
            || (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && IsFacingRight)) && !IsWallJumping)
            _lastOnWallLeftTime = Data.coyoteTime;

        //Two checks needed for both left and right walls since whenever the play turns the wall checkPoints swap sides
        _lastOnWallTime = Mathf.Max(_lastOnWallLeftTime, _lastOnWallRightTime);
        #endregion

        #region JUMP CHECKS
        // Falling down
        if (IsJumping && _rb.velocity.y < 0)
        {
            IsJumping = false;

            if (!IsWallJumping)
                _isJumpFalling = true;
        }
        if (IsWallJumping && Time.time - _wallJumpStartTime > Data.wallJumpTime)
        {
            IsWallJumping = false;
        }
        // Too late for jump cut
        if (_lastOnGroundTime > 0 && !IsJumping && !IsWallJumping)
        {
            _isJumpCut = false;

            if (!IsJumping)
                _isJumpFalling = false;
        }

        // Jump
        if (CanJump() && _lastPressedJumpTime > 0)
        {
            IsJumping = true;
            IsWallJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;
            Jump();
        }
        // Wall jump
        else if (CanWallJump() && _lastPressedJumpTime > 0)
        {
            IsWallJumping = true;
            IsJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;

            _wallJumpStartTime = Time.time;
            _lastWallJumpDir = (_lastOnWallRightTime > 0) ? -1 : 1;

            WallJump(_lastWallJumpDir);
        }
        #endregion

        #region SLIDE CHECKS
        if (CanSlide() && ((_lastOnWallLeftTime > 0 && _moveInput.x < 0) || (_lastOnWallRightTime > 0 && _moveInput.x > 0)))
            IsSliding = true;
        else
            IsSliding = false;
        #endregion

        #region GRAVITY
        if (IsSliding)
        {
            SetGravityScale(0);
        }
        else if (_rb.velocity.y < 0 && _moveInput.y < 0)
        {
            // Much higher gravity if holding down
            SetGravityScale(Data.gravityScale * Data.fastFallGravityMult);
            // Caps maximum fall speed
            _rb.velocity = new Vector2(_rb.velocity.x, Mathf.Max(_rb.velocity.y, -Data.maxFastFallSpeed));
        }
        else if (_isJumpCut)
        {
            // Higher gravity if jump button released
            SetGravityScale(Data.gravityScale * Data.jumpCutGravityMult);
            _rb.velocity = new Vector2(_rb.velocity.x, Mathf.Max(_rb.velocity.y, -Data.maxFallSpeed));
        }
        else if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(_rb.velocity.y) < Data.jumpHangTimeThreshold)
        {
            // Smaller gravity when close to the apex
            SetGravityScale(Data.gravityScale * Data.jumpHangGravityMult);
        }
        else if (_rb.velocity.y < 0)
        {
            // Higher gravity if falling
            SetGravityScale(Data.gravityScale * Data.fallGravityMult);
            // Caps maximum fall speed
            _rb.velocity = new Vector2(_rb.velocity.x, Mathf.Max(_rb.velocity.y, -Data.maxFallSpeed));
        }
        else
        {
            // Default gravity
            SetGravityScale(Data.gravityScale);
        }
        #endregion

    }
    private void FixedUpdate()
    {
        // Run
        if (IsWallJumping)
            Run(Data.wallJumpRunLerp);
        else
            Run(1);
        
        // Slide
        if (IsSliding)
            Slide();
        _animator.SetBool("IsRunning", _moveInput.x != 0);
    }

    #region INPUT CALLBACKS
    public void OnJumpInput()
    {
        _lastPressedJumpTime = Data.jumpInputBufferTime;
    }
    public void OnJumpUpInput()
    {
        if (CanJumpCut() || CanWallJumpCut())
            _isJumpCut = true;
    }
    #endregion

    #region RUN METHODS
    private void Run(float lerpAmount)
    {
        float targetSpeed = _moveInput.x * Data.runMaxSpeed;
        targetSpeed = Mathf.Lerp(_rb.velocity.x, targetSpeed, lerpAmount);

        #region Calculate AccelRate
        float accelRate;

        // Gets an acceleration value based on if we are accelerating (includes turning) 
        // or trying to decelerate (stop). As well as applying a multiplier if we're air borne.
        if (_lastOnGroundTime > 0)
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount : Data.runDecceleration;
        else
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runDeccelAmount * Data.accelInAir : Data.runDecceleration * Data.deccelInAir;
        #endregion

        // Increase are acceleration and maxSpeed when at the apex of the jump
        if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(_rb.velocity.y) < Data.jumpHangTimeThreshold)
        {
            accelRate *= Data.jumpHangAccelerationMult;
            targetSpeed *= Data.jumpHangMaxSpeedMult;
        }

        #region Conserve Momentum
        // We won't slow the player down if they are moving in their desired direction but at a greater speed than their maxSpeed
        if (Data.doConserveMomentum && Mathf.Abs(_rb.velocity.x) > Mathf.Abs(targetSpeed) && Mathf.Sign(_rb.velocity.x) == Mathf.Sign(targetSpeed) && Mathf.Abs(targetSpeed) > 0.01f && _lastOnGroundTime < 0)
        {
            // Prevent any deceleration from happening, or in other words conserve current momentum
            accelRate = 0;
        }
        #endregion

        float speedDif = targetSpeed - _rb.velocity.x;

        //float movement = speedDif * accelRate; <-- previous methods
        float movement;

        if (_IsOnPlatform)
        {
            movement = (speedDif + _platformRBody2D.velocity.x) * accelRate;
        } else
        {
            movement = speedDif * accelRate;
        }

        _rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
    }

    private void Turn()
    {
        // Stores scale and flips the player along the x axis, 
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;

        IsFacingRight = !IsFacingRight;
    }
    #endregion

    #region JUMP METHODS
    private void Jump()
    {
        _lastPressedJumpTime = 0;
        _lastOnGroundTime = 0;

        float force = Data.jumpForce;
        if (_rb.velocity.y < 0)
            force -= _rb.velocity.y;

        _rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);
    }
    private void WallJump(int dir)
    {
        // Ensures we can't call Wall Jump multiple times from one press
        _lastPressedJumpTime = 0;
        _lastOnGroundTime = 0;
        _lastOnWallRightTime = 0;
        _lastOnWallLeftTime = 0;

        #region Perform Wall Jump
        Vector2 force = new Vector2(Data.wallJumpForce.x, Data.wallJumpForce.y);
        force.x *= dir; 
        
        // Apply force in opposite direction of wall
        if (Mathf.Sign(_rb.velocity.x) != Mathf.Sign(force.x))
            force.x -= _rb.velocity.x;

        if (_rb.velocity.y < 0) // checks whether player is falling, if so we subtract the velocity.y (counteracting force of gravity). This ensures the player always reaches our desired jump force or greater
            force.y -= _rb.velocity.y;

        _rb.AddForce(force, ForceMode2D.Impulse);
        #endregion
    }
    #endregion

    #region OTHER MOVEMENT METHODS
    private void Slide()
    {
        // Works the same as the Run but only in the y-axis
        float speedDif = Data.slideSpeed - _rb.velocity.y;
        float movement = speedDif * Data.slideAccel;
        //So, we clamp the movement here to prevent any over corrections (these aren't noticeable in the Run)
        //The force applied can't be greater than the (negative) speedDifference * by how many times a second FixedUpdate() is called
        movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime), Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));

        _rb.AddForce(movement * Vector2.up);
    }
    #endregion

    #region GENERAL METHODS
    private void SetGravityScale(float scale)
    {
        _rb.gravityScale = scale;
    }
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Platform")
        {
            _platformRBody2D = collision.gameObject.GetComponent<Rigidbody2D>();
            _IsOnPlatform = true;
        }
    }
    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Platform")
        {
            _IsOnPlatform = false;
            _platformRBody2D = null;
        }
    }

    void LoadGame(GameData data)
    {
        transform.position = data.playerPosition;
    }
    void SaveGame(GameData data)
    {
        data.playerPosition = transform.position;
    }

    #endregion

    #region CHECK METHODS
    public void CheckDirectionToFace(bool isMovingRight)
    {
        if (isMovingRight != IsFacingRight)
            Turn();
    }
    private bool CanJump()
    {
        return _lastOnGroundTime > 0 && !IsJumping;
    }
    private bool CanWallJump()
    {
        return _lastPressedJumpTime > 0 && _lastOnWallTime > 0 && _lastOnGroundTime <= 0 && (!IsWallJumping ||
             (_lastOnWallRightTime > 0 && _lastWallJumpDir == 1) || (_lastOnWallLeftTime > 0 && _lastWallJumpDir == -1));
    }
    private bool CanJumpCut()
    {
        return IsJumping && _rb.velocity.y > 0;
    }
    private bool CanWallJumpCut()
    {
        return IsWallJumping && _rb.velocity.y > 0;
    }
    public bool CanSlide()
    {
        if (_lastOnWallTime > 0 && !IsJumping && !IsWallJumping && _lastOnGroundTime <= 0)
            return true;
        else
            return false;
    }
    #endregion

    private void OnDrawGizmos()
    {
        /*Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(1, 1, 1));*/

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_groundCheckPoint.position, _groundCheckSize);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(_frontWallCheckPoint.position, _wallCheckSize);
        Gizmos.DrawWireCube(_backWallCheckPoint.position, _wallCheckSize);
    }
}