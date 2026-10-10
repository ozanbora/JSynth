using System;
using System.Collections.Generic;

namespace JSynth.Model
{
    /// <summary>
    /// Names Julia already has: those of Base, Core and the standard libraries designs use (Julia 1.13, names(m)). A
    /// variable named after one of them would hide it from the functions of the script (length = 1.0, and length(v)
    /// fails), so a node named so gets another variable.
    /// </summary>
    internal static class JuliaNames
    {
        public static IReadOnlySet<string> Taken { get; } = new HashSet<string>(
            """
            ARGS AbstractArray AbstractChannel AbstractChar AbstractDict AbstractDisplay AbstractFloat
            AbstractIrrational AbstractLock AbstractMatch AbstractMatrix AbstractOneTo AbstractPattern AbstractPipe
            AbstractRNG AbstractRange AbstractSet AbstractSlices AbstractSparseArray AbstractSparseMatrix
            AbstractSparseVector AbstractString AbstractTriangular AbstractUnitRange AbstractVecOrMat AbstractVector
            Adjoint Any Apr April ArgumentError Array AssertionError AsyncCondition AtomicMemory AtomicMemoryRef Aug
            August BLAS Base Bidiagonal BigFloat BigInt BitArray BitMatrix BitSet BitVector Bool BoundsError
            Broadcast BufferStream BunchKaufman C_NULL CanonicalIndexError CapturedException CartesianIndex
            CartesianIndices Cchar Cdouble Cfloat Channel Char Checked Cholesky CholeskyPivoted Cint Cintmax_t Clong
            Clonglong Cmd CodeUnits Colon ColumnNorm ColumnSlices Complex ComplexF16 ComplexF32 ComplexF64
            ComposedFunction CompositeException ConcurrencyViolationError Condition Core Cptrdiff_t Cshort Csize_t
            Cssize_t Cstring Cuchar Cuint Cuintmax_t Culong Culonglong Cushort Cvoid Cwchar_t Cwstring DEPOT_PATH
            DL_LOAD_PATH DataType Date DateFormat DatePeriod DateTime Dates Day Dec December DelimitedFiles
            DenseArray DenseMatrix DenseVecOrMat DenseVector Diagonal Dict DimensionMismatch Dims DivideError Docs
            DomainError ENDIAN_BOM ENV EOFError Eigen Enum ErrorException Event Exception ExponentialBackOff Expr
            Factorization Feb February FieldError Filesystem Fix Fix1 Fix2 Float16 Float32 Float64 Format Fri Friday
            Function GC GeneralizedEigen GeneralizedSVD GeneralizedSchur Generator GenericMemory GenericMemoryRef
            Givens GlobalRef HTML Hermitian Hessenberg Hour I IO IOBuffer IOContext IOServer IOStream IR
            ISODateFormat ISODateTimeFormat ISOTimeFormat IdDict IdSet ImmutableDict IndexCartesian IndexLinear
            IndexStyle InexactError Inf Inf16 Inf32 Inf64 InitError InsertionSort Int Int128 Int16 Int32 Int64 Int8
            Integer InterruptException InvalidStateException Irrational IteratorEltype IteratorSize Iterators Jan
            January Jul July Jun June KeyError LAPACK LAPACKException LDLt LOAD_PATH LQ LU LazyString Libc LinRange
            LineNumberNode LinearAlgebra LinearIndices LoadError Lockable LogRange LowerTriangular MIME Main Mar
            March MathConstants Matrix May Memory MemoryRef MergeSort MersenneTwister Meta Method MethodError
            Microsecond Millisecond Minute Missing MissingException Module Mon Monday Month NTuple NaN NaN16 NaN32
            NaN64 NamedTuple Nanosecond NoPivot Nothing Nov November Number OS_HANDLE Oct October OncePerProcess
            OncePerTask OncePerThread OneTo Order OrdinalRange OutOfMemoryError OverflowError PROGRAM_FILE Pair
            Pairs PartialQuickSort Period PermutedDimsArray Pipe PipeBuffer PipeEndpoint PosDefException Printf
            ProcessFailedException Ptr QR QRPivoted Quarter QuickSort QuoteNode RFC1123Format Random RandomDevice
            RankDeficientException Rational RawFD ReadOnlyMemoryError Real ReentrantLock Ref Regex RegexMatch
            Returns RoundDown RoundFromZero RoundNearest RoundNearestTiesAway RoundNearestTiesUp RoundToZero RoundUp
            RoundingMode RowMaximum RowNonZero RowSlices SVD Sampler SamplerSimple SamplerTrivial SamplerType Sat
            Saturday Schur ScopedValues Second SegmentationFault Semaphore Sep September Set Signed
            SingularException Slices Some Sort SparseArrays SparseMatrixCSC SparseVector StackOverflowError
            StackTraces Statistics StepRange StepRangeLen StridedArray StridedMatrix StridedVecOrMat StridedVector
            String StringIndexError SubArray SubString SubstitutionString Sun Sunday SymTridiagonal Symbol Symmetric
            Sys SystemError TTY Task TaskFailedException TaskLocalRNG Text TextDisplay Threads Thu Thursday Time
            TimePeriod TimeType TimeZone Timer Transpose Tridiagonal Tue Tuesday Tuple Type TypeError TypeVar UInt
            UInt128 UInt16 UInt32 UInt64 UInt8 UTC UUID UndefInitializer UndefKeywordError UndefRefError
            UndefVarError UniformScaling Union UnionAll UnitLowerTriangular UnitRange UnitUpperTriangular Unsigned
            UpperHessenberg UpperTriangular VERSION Val Vararg VecElement VecOrMat Vector VersionNumber WeakKeyDict
            WeakRef Wed Wednesday Week Xoshiro Year ZeroPivotException __has_internal_change __precompile__ abs abs2
            abspath accumulate accumulate! acos acosd acosh acot acotd acoth acquire acsc acscd acsch
            active_manifest active_project addenv adjoint adjoint! all all! allequal allunique angle any any!
            append! applicable argmax argmin arrayref arrayset arraysize ascii asec asecd asech asin asind asinh
            asyncmap asyncmap! atan atand atanh atexit atreplinit axes axpby! axpy! backtrace basename big bind
            binomial bitrand bitreverse bitrotate bitstring blockdiag broadcast broadcast! bswap bunchkaufman
            bunchkaufman! bytes2hex bytesavailable canonicalize cat catch_backtrace cbrt cconvert cd ceil cglobal
            checkbounds checked_length checkindex checksquare chmod cholesky cholesky! chomp chop chopprefix
            chopsuffix chown circcopy! circshift circshift! cis cispi clamp clamp! cld close closewrite cmp coalesce
            code_lowered code_typed codepoint codeunit codeunits collect complex cond condskeel conj conj!
            const_arrayref contains contractuser convert copy copy! copy_adjoint! copy_transpose! copyline copysign
            copyto! copytrito! copyuntil cor cos cosc cosd cosh cospi cot cotd coth count count! count_ones
            count_zeros countlines cov cp cross csc cscd csch ctime ctruncate cumprod cumprod! cumsum cumsum!
            current_exceptions current_task datetime2julian datetime2rata datetime2unix day dayabbr dayname
            dayofmonth dayofquarter dayofweek dayofweekofmonth dayofyear daysinmonth daysinyear daysofweekinmonth
            deepcopy default_rng deg2rad delete! deleteat! denominator depwarn det detach devnull diag diagind diagm
            diagview diff digits digits! dirname disable_sigint diskstat display displayable displaysize div divrem
            donotdelete dot download dropdims droptol! dropzeros dropzeros! dump dup eachcol eachindex eachline
            eachmatch eachrow eachrsplit eachslice eachsplit eigen eigen! eigmax eigmin eigvals eigvals! eigvecs
            elsize eltype empty empty! endswith enumerate eof eps error errormonitor esc escape_microsoft_c_args
            escape_raw_string escape_string evalfile evalpoly exit exit_on_sigint exp exp10 exp2 expanduser expm1
            exponent extrema extrema! factorial factorize falses fd fdio fetch fieldcount fieldindex fieldname
            fieldnames fieldoffset fieldtype fieldtypes filemode filesize fill fill! fillband! fillstored! filter
            filter! finalize finalizer findall findfirst findlast findmax findmax! findmin findmin! findnext findnz
            findprev first firstdayofmonth firstdayofquarter firstdayofweek firstdayofyear firstindex fkeep! fld
            fld1 fldmod fldmod1 flipsign float floatmax floatmin floor flush fma foldl foldr foreach format
            fourthroot frexp front ftranspose! fullname functionloc gcd gcdx gensym get get! get_extension
            get_zero_subnormals getfield getglobal gethostname getindex getkey getpid getproperty givens gperm
            hardlink has_offset_axes hasfield hash haskey hasmethod hasproperty haszero hcat hermitian
            hermitian_type hermitianpart hermitianpart! hessenberg hessenberg! hex2bytes hex2bytes! homedir hour
            htol hton hvcat hvncat hypot identify_package identity ifelse ignorestatus im imag in in!
            include_dependency include_string indexin inertia insert! insertdims insorted instances intersect
            intersect! inv invmod invoke invokelatest invperm invpermute! isa isa_ast_node isabspath isabstracttype
            isambiguous isapprox isascii isassigned isbanded isbinaryoperator isbits isbitstype isblockdev ischardev
            iscntrl isconcretetype isconst isdefined isdefinedglobal isdiag isdigit isdir isdirpath isdisjoint
            isdispatchtuple isdone isempty isequal iseven isexecutable isexported isexpr isfifo isfile isfinite
            isfull ishermitian isidentifier isimmutable isinf isinteger isinteractive isleapyear isless isletter
            islink islocked islowercase ismarked ismissing ismount ismutable ismutabletype isnan isnegative
            isnothing isnumeric isodd isone isopen isoperator isoweekdate isoyear ispath isperm isposdef isposdef!
            ispositive ispow2 isprimitivetype isprint ispublic ispunct isqrt isreadable isreadonly isready isreal
            issetequal issetgid issetuid issingletontype issocket issorted isspace issparse issticky isstructtype
            issubnormal issubset issuccess issymmetric istaskdone istaskfailed istaskstarted istextmime istril
            istriu isunaryoperator isunordered isuppercase isvalid iswritable isxdigit iszero iterate
            jit_total_bytes join joinpath julian2datetime keepat! keys keytype kill kron kron! last lastdayofmonth
            lastdayofquarter lastdayofweek lastdayofyear lastindex lcm ldexp ldiv! ldlt ldlt! leading_ones
            leading_zeros length link_pipe! lmul! load_path locate_package lock log log10 log1p log2 logabsdet
            logdet logrange lowercase lowercasefirst lowrankdowndate lowrankdowndate! lowrankupdate lowrankupdate!
            lpad lq lq! lstat lstrip ltoh ltruncate lu lu! lyap macroexpand macroexpand! map map! mapfoldl mapfoldr
            mapreduce mapslices mark match matprod_dest max maximum maximum! maxintfloat mean mean! median median!
            memoryindex memoryref merge merge! mergewith mergewith! methods microsecond middle millisecond min
            minimum minimum! minmax minute missing mkdir mkpath mktemp mktempdir mod mod1 mod2pi modf modifyfield!
            modifyglobal! modifyproperty! moduleroot month monthabbr monthday monthname mtime mul! mul_hi muladd mv
            nameof names nand nanosecond ncodeunits ndigits ndims nextfloat nextind nextpow nextprod nfields nnz
            nonmissingtype nonzeros nor norm normalize normalize! normpath nothing notify notnothing now ntoh ntuple
            nullspace numerator nzrange objectid occursin oftype one ones oneunit only open operator_associativity
            operator_precedence operm opnorm ordschur ordschur! pairs parent parentindices parentmodule parse
            partialsort partialsort! partialsortperm partialsortperm! pathof peakflops peek permute permute!
            permutedims permutedims! pi pinv pipeline pkgdir pkgversion pointer pointer_from_objref pop! popat!
            popdisplay popfirst! position powermod precision precompile prepend! prevfloat prevind prevpow print
            println printstyled process_exited process_running prod prod! promote promote_rule promote_shape
            promote_type propertynames push! pushdisplay pushfirst! put! pwd qr qr! quantile quantile! quarterofyear
            quoted rad2deg rand rand! randcycle randcycle! randexp randexp! randn randn! randperm randperm!
            randstring randsubseq randsubseq! range rank rata2datetime rationalize rdiv! read read! readavailable
            readbytes! readchomp readdir readdlm readeach readline readlines readlink readuntil real realpath
            redirect_stderr redirect_stdin redirect_stdio redirect_stdout redisplay reduce reenable_sigint reflect!
            reim reinterpret release relpath rem rem2pi remove_linenums! rename repeat replace replace!
            replacefield! replaceglobal! replaceproperty! repr require_one_based_indexing reset reseteof reshape
            resize! rest rethrow retry reverse reverse! reverseind rm rmul! rot180 rotate! rotl90 rotr90 round
            rounding rowvals rpad rsplit rstrip rtruncate run runtests samefile schedule schur schur! searchsorted
            searchsortedfirst searchsortedlast sec secd sech second seed! seek seekend seekstart selectdim
            set_zero_subnormals setcpuaffinity setdiff setdiff! setenv setfield! setfieldonce! setgid setglobal!
            setglobalonce! setindex! setprecision setproperty! setpropertyonce! setrounding setuid shell_escape
            shell_escape_csh shell_escape_posixly shell_escape_wincmd shell_split show showable showarg showerror
            shuffle shuffle! sign signbit signed significand similar sin sinc sincos sincosd sincospi sind sinh
            sinpi size sizehint! sizeof skip skipchars skipmissing sleep something sort sort! sortperm sortperm!
            sortslices sparse sparse_hcat sparse_hvcat sparse_vcat sparsevec spdiagm splat splice! split split_rest
            splitdir splitdrive splitext splitpath sprand sprandn sprint spzeros sqrt stack stacktrace startswith
            stat std stderr stdin stdm stdout step stride strides string strip success sum sum! summary summarysize
            supertype svd svd! svdvals svdvals! swapfield! swapglobal! swapproperty! sylvester symdiff symdiff!
            symlink symmetric symmetric_type systemerror tail take! takestring! tan tand tanh tanpi
            task_local_storage tempdir tempname text_colors textwidth thisind throw time time_ns timedwait titlecase
            to_index to_indices today tofirst tolast tonext toprev touch tr trailing_ones trailing_zeros transcode
            transpose transpose! tril tril! triu triu! trues trunc truncate trylock tryparse tuple typeassert
            typeintersect typejoin typemax typemin typeof uabs undef unescape_string union union! unique unique!
            unix2datetime unlock unmark unsafe_convert unsafe_copyto! unsafe_load unsafe_modify!
            unsafe_pointer_to_objref unsafe_read unsafe_replace! unsafe_store! unsafe_string unsafe_swap!
            unsafe_trunc unsafe_wrap unsafe_write unsigned uperm uplo uppercase uppercasefirst valtype values var
            varm vcat vec vect view wait waitall waitany walkdir week weeksinyear which widemul widen windowserror
            withenv write writedlm xor year yearmonth yearmonthday yield yieldto zero zeros zeroslike zip
            """.Split((char[])[' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);
    }
}