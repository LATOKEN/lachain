; ModuleID = 'FullMath'
source_filename = "examples/uniswapV3core/libraries/FullMath.sol"
target datalayout = "e-m:e-p:32:32-i64:64-n32:64-S128"
target triple = "wasm32-unknown-unknown-wasm"

%struct.vector = type { i32, i32, [0 x i8] }
%struct.chunk = type { %struct.chunk*, %struct.chunk*, i32, i32 }
%struct.SHA3_CTX = type { [25 x i64], [24 x i64], i16 }

@uint256_0 = hidden local_unnamed_addr constant i256 0, align 8
@uint256_1 = hidden local_unnamed_addr constant i256 1, align 8
@uint512_0 = hidden local_unnamed_addr constant i512 0, align 8
@uint512_1 = hidden local_unnamed_addr constant i512 1, align 8
@constants = hidden local_unnamed_addr constant [72 x i8] c"\01\1A^p\1F!yU\0E\0C5&?O]SRH\16fyX!t\01\06\09\16\0E\14\02\0C\0D\13\17\0F\04\18\15\08\10\05\03\12\11\0B\07\0A\01>\1C\1B$,\067\14\03\0A+\19')-\0F\15\08\12\02=8\0E", align 16
@selector = internal global i32 0
@calldata_len = internal global i32 0
@calldata_data = internal global i8* null

; Function Attrs: nofree norecurse nounwind writeonly
define internal void @__memset8(i8* nocapture %_dest, i64 %val, i32 %length) local_unnamed_addr #0 {
entry:
  %0 = bitcast i8* %_dest to i64*
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %length.addr.0 = phi i32 [ %length, %entry ], [ %dec, %do.body ]
  %dest.0 = phi i64* [ %0, %entry ], [ %incdec.ptr, %do.body ]
  %incdec.ptr = getelementptr inbounds i64, i64* %dest.0, i32 1
  store i64 %val, i64* %dest.0, align 8, !tbaa !3
  %dec = add i32 %length.addr.0, -1
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  ret void
}

; Function Attrs: nofree norecurse nounwind writeonly
define internal void @__memset(i8* nocapture %_dest, i8 zeroext %val, i32 %length) local_unnamed_addr #0 {
entry:
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %length.addr.0 = phi i32 [ %length, %entry ], [ %dec, %do.body ]
  %dest.0 = phi i8* [ %_dest, %entry ], [ %incdec.ptr, %do.body ]
  %incdec.ptr = getelementptr inbounds i8, i8* %dest.0, i32 1
  store i8 %val, i8* %dest.0, align 1, !tbaa !7
  %dec = add i32 %length.addr.0, -1
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  ret void
}

; Function Attrs: nofree norecurse nounwind
define internal void @__memcpy8(i8* nocapture %_dest, i8* nocapture readonly %_src, i32 %length) local_unnamed_addr #1 {
entry:
  %0 = bitcast i8* %_dest to i64*
  %1 = bitcast i8* %_src to i64*
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %length.addr.0 = phi i32 [ %length, %entry ], [ %dec, %do.body ]
  %dest.0 = phi i64* [ %0, %entry ], [ %incdec.ptr1, %do.body ]
  %src.0 = phi i64* [ %1, %entry ], [ %incdec.ptr, %do.body ]
  %incdec.ptr = getelementptr inbounds i64, i64* %src.0, i32 1
  %2 = load i64, i64* %src.0, align 8, !tbaa !3
  %incdec.ptr1 = getelementptr inbounds i64, i64* %dest.0, i32 1
  store i64 %2, i64* %dest.0, align 8, !tbaa !3
  %dec = add i32 %length.addr.0, -1
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  ret void
}

; Function Attrs: nofree norecurse nounwind
define internal void @__memcpy(i8* nocapture %_dest, i8* nocapture readonly %_src, i32 %length) local_unnamed_addr #1 {
entry:
  %tobool.not4 = icmp eq i32 %length, 0
  br i1 %tobool.not4, label %while.end, label %while.body

while.body:                                       ; preds = %entry, %while.body
  %src.07 = phi i8* [ %incdec.ptr, %while.body ], [ %_src, %entry ]
  %dest.06 = phi i8* [ %incdec.ptr1, %while.body ], [ %_dest, %entry ]
  %length.addr.05 = phi i32 [ %dec, %while.body ], [ %length, %entry ]
  %dec = add i32 %length.addr.05, -1
  %incdec.ptr = getelementptr inbounds i8, i8* %src.07, i32 1
  %0 = load i8, i8* %src.07, align 1, !tbaa !7
  %incdec.ptr1 = getelementptr inbounds i8, i8* %dest.06, i32 1
  store i8 %0, i8* %dest.06, align 1, !tbaa !7
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %while.end, label %while.body

while.end:                                        ; preds = %while.body, %entry
  ret void
}

; Function Attrs: nofree norecurse nounwind writeonly
define internal void @__bzero8(i8* nocapture %_dest, i32 %length) local_unnamed_addr #0 {
entry:
  %tobool.not2 = icmp eq i32 %length, 0
  br i1 %tobool.not2, label %while.end, label %while.body.preheader

while.body.preheader:                             ; preds = %entry
  %0 = bitcast i8* %_dest to i64*
  br label %while.body

while.body:                                       ; preds = %while.body.preheader, %while.body
  %dest.04 = phi i64* [ %incdec.ptr, %while.body ], [ %0, %while.body.preheader ]
  %length.addr.03 = phi i32 [ %dec, %while.body ], [ %length, %while.body.preheader ]
  %dec = add i32 %length.addr.03, -1
  %incdec.ptr = getelementptr inbounds i64, i64* %dest.04, i32 1
  store i64 0, i64* %dest.04, align 8, !tbaa !3
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %while.end, label %while.body

while.end:                                        ; preds = %while.body, %entry
  ret void
}

; Function Attrs: nofree norecurse nounwind
define internal void @__be32toleN(i8* nocapture readonly %from, i8* nocapture %to, i32 %length) local_unnamed_addr #1 {
entry:
  %add.ptr = getelementptr inbounds i8, i8* %from, i32 31
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %from.addr.0 = phi i8* [ %add.ptr, %entry ], [ %incdec.ptr, %do.body ]
  %to.addr.0 = phi i8* [ %to, %entry ], [ %incdec.ptr1, %do.body ]
  %length.addr.0 = phi i32 [ %length, %entry ], [ %dec, %do.body ]
  %incdec.ptr = getelementptr inbounds i8, i8* %from.addr.0, i32 -1
  %0 = load i8, i8* %from.addr.0, align 1, !tbaa !7
  %incdec.ptr1 = getelementptr inbounds i8, i8* %to.addr.0, i32 1
  store i8 %0, i8* %to.addr.0, align 1, !tbaa !7
  %dec = add i32 %length.addr.0, -1
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  ret void
}

; Function Attrs: nofree norecurse nounwind
define internal void @__beNtoleN(i8* nocapture readonly %from, i8* nocapture %to, i32 %length) local_unnamed_addr #1 {
entry:
  %add.ptr = getelementptr inbounds i8, i8* %from, i32 %length
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %from.addr.0 = phi i8* [ %add.ptr, %entry ], [ %incdec.ptr, %do.body ]
  %to.addr.0 = phi i8* [ %to, %entry ], [ %incdec.ptr1, %do.body ]
  %length.addr.0 = phi i32 [ %length, %entry ], [ %dec, %do.body ]
  %incdec.ptr = getelementptr inbounds i8, i8* %from.addr.0, i32 -1
  %0 = load i8, i8* %incdec.ptr, align 1, !tbaa !7
  %incdec.ptr1 = getelementptr inbounds i8, i8* %to.addr.0, i32 1
  store i8 %0, i8* %to.addr.0, align 1, !tbaa !7
  %dec = add i32 %length.addr.0, -1
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  ret void
}

; Function Attrs: nofree norecurse nounwind
define internal void @__leNtobe32(i8* nocapture readonly %from, i8* nocapture %to, i32 %length) local_unnamed_addr #1 {
entry:
  %add.ptr = getelementptr inbounds i8, i8* %to, i32 31
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %from.addr.0 = phi i8* [ %from, %entry ], [ %incdec.ptr, %do.body ]
  %to.addr.0 = phi i8* [ %add.ptr, %entry ], [ %incdec.ptr1, %do.body ]
  %length.addr.0 = phi i32 [ %length, %entry ], [ %dec, %do.body ]
  %incdec.ptr = getelementptr inbounds i8, i8* %from.addr.0, i32 1
  %0 = load i8, i8* %from.addr.0, align 1, !tbaa !7
  %incdec.ptr1 = getelementptr inbounds i8, i8* %to.addr.0, i32 -1
  store i8 %0, i8* %to.addr.0, align 1, !tbaa !7
  %dec = add i32 %length.addr.0, -1
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  ret void
}

; Function Attrs: nofree norecurse nounwind
define internal void @__leNtobeN(i8* nocapture readonly %from, i8* nocapture %to, i32 %length) local_unnamed_addr #1 {
entry:
  %add.ptr = getelementptr inbounds i8, i8* %to, i32 %length
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %from.addr.0 = phi i8* [ %from, %entry ], [ %incdec.ptr, %do.body ]
  %to.addr.0 = phi i8* [ %add.ptr, %entry ], [ %incdec.ptr1, %do.body ]
  %length.addr.0 = phi i32 [ %length, %entry ], [ %dec, %do.body ]
  %incdec.ptr = getelementptr inbounds i8, i8* %from.addr.0, i32 1
  %0 = load i8, i8* %from.addr.0, align 1, !tbaa !7
  %incdec.ptr1 = getelementptr inbounds i8, i8* %to.addr.0, i32 -1
  store i8 %0, i8* %incdec.ptr1, align 1, !tbaa !7
  %dec = add i32 %length.addr.0, -1
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  ret void
}

; Function Attrs: nofree norecurse nounwind
define internal nonnull i8* @__u256ptohex(i8* nocapture readonly %v, i8* %str) local_unnamed_addr #1 {
entry:
  %add.ptr = getelementptr inbounds i8, i8* %str, i32 63
  br label %for.body

for.cond.cleanup:                                 ; preds = %for.body
  ret i8* %incdec.ptr24

for.body:                                         ; preds = %for.body, %entry
  %str.addr.036 = phi i8* [ %add.ptr, %entry ], [ %incdec.ptr24, %for.body ]
  %i.035 = phi i32 [ 0, %entry ], [ %inc, %for.body ]
  %arrayidx = getelementptr inbounds i8, i8* %v, i32 %i.035
  %0 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %1 = and i8 %0, 15
  %cmp3 = icmp ugt i8 %1, 9
  %add = add nuw nsw i8 %1, 97
  %add7 = or i8 %1, 48
  %cond = select i1 %cmp3, i8 %add, i8 %add7
  %incdec.ptr = getelementptr inbounds i8, i8* %str.addr.036, i32 -1
  store i8 %cond, i8* %str.addr.036, align 1, !tbaa !7
  %2 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %3 = lshr i8 %2, 4
  %cmp13 = icmp ugt i8 %2, -97
  %add17 = add nuw nsw i8 %3, 97
  %add20 = or i8 %3, 48
  %cond22 = select i1 %cmp13, i8 %add17, i8 %add20
  %incdec.ptr24 = getelementptr inbounds i8, i8* %str.addr.036, i32 -2
  store i8 %cond22, i8* %incdec.ptr, align 1, !tbaa !7
  %inc = add nuw nsw i32 %i.035, 1
  %exitcond.not = icmp eq i32 %inc, 32
  br i1 %exitcond.not, label %for.cond.cleanup, label %for.body
}

; Function Attrs: nounwind
define internal %struct.vector* @vector_new(i32 %members, i32 %size, i8* readonly %initial) local_unnamed_addr #2 {
entry:
  %mul = mul i32 %size, %members
  %add = add i32 %mul, 8
  %call = tail call i8* @__malloc(i32 %add) #14
  %len = bitcast i8* %call to i32*
  store i32 %members, i32* %len, align 4, !tbaa !8
  %size1 = getelementptr inbounds i8, i8* %call, i32 4
  %0 = bitcast i8* %size1 to i32*
  store i32 %members, i32* %0, align 4, !tbaa !8
  %data2 = getelementptr inbounds i8, i8* %call, i32 8
  %cmp.not = icmp eq i8* %initial, inttoptr (i32 -1 to i8*)
  %tobool6.not23 = icmp eq i32 %mul, 0
  br i1 %cmp.not, label %while.cond4.preheader, label %while.cond.preheader

while.cond.preheader:                             ; preds = %entry
  br i1 %tobool6.not23, label %if.end, label %while.body

while.cond4.preheader:                            ; preds = %entry
  br i1 %tobool6.not23, label %if.end, label %while.body7

while.body:                                       ; preds = %while.cond.preheader, %while.body
  %data.029 = phi i8* [ %incdec.ptr3, %while.body ], [ %data2, %while.cond.preheader ]
  %size_array.028 = phi i32 [ %dec, %while.body ], [ %mul, %while.cond.preheader ]
  %initial.addr.027 = phi i8* [ %incdec.ptr, %while.body ], [ %initial, %while.cond.preheader ]
  %dec = add i32 %size_array.028, -1
  %incdec.ptr = getelementptr inbounds i8, i8* %initial.addr.027, i32 1
  %1 = load i8, i8* %initial.addr.027, align 1, !tbaa !7
  %incdec.ptr3 = getelementptr inbounds i8, i8* %data.029, i32 1
  store i8 %1, i8* %data.029, align 1, !tbaa !7
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %if.end, label %while.body

while.body7:                                      ; preds = %while.cond4.preheader, %while.body7
  %data.125 = phi i8* [ %incdec.ptr8, %while.body7 ], [ %data2, %while.cond4.preheader ]
  %size_array.124 = phi i32 [ %dec5, %while.body7 ], [ %mul, %while.cond4.preheader ]
  %dec5 = add i32 %size_array.124, -1
  %incdec.ptr8 = getelementptr inbounds i8, i8* %data.125, i32 1
  store i8 0, i8* %data.125, align 1, !tbaa !7
  %tobool6.not = icmp eq i32 %dec5, 0
  br i1 %tobool6.not, label %if.end, label %while.body7

if.end:                                           ; preds = %while.body, %while.body7, %while.cond.preheader, %while.cond4.preheader
  %2 = bitcast i8* %call to %struct.vector*
  ret %struct.vector* %2
}

; Function Attrs: noinline nounwind
define internal nonnull i8* @__malloc(i32 %size) local_unnamed_addr #3 {
entry:
  br label %land.rhs

land.rhs:                                         ; preds = %while.body, %entry
  %cur.012 = phi %struct.chunk* [ inttoptr (i32 65536 to %struct.chunk*), %entry ], [ %2, %while.body ]
  %allocated = getelementptr inbounds %struct.chunk, %struct.chunk* %cur.012, i32 0, i32 3
  %0 = load i32, i32* %allocated, align 4, !tbaa !10
  %tobool1.not = icmp eq i32 %0, 0
  br i1 %tobool1.not, label %lor.rhs, label %while.body

lor.rhs:                                          ; preds = %land.rhs
  %length = getelementptr inbounds %struct.chunk, %struct.chunk* %cur.012, i32 0, i32 2
  %1 = load i32, i32* %length, align 4, !tbaa !14
  %cmp = icmp ult i32 %1, %size
  br i1 %cmp, label %while.body, label %while.end

while.body:                                       ; preds = %land.rhs, %lor.rhs
  %next = getelementptr inbounds %struct.chunk, %struct.chunk* %cur.012, i32 0, i32 0
  %2 = load %struct.chunk*, %struct.chunk** %next, align 4, !tbaa !15
  %tobool.not = icmp eq %struct.chunk* %2, null
  br i1 %tobool.not, label %while.body.while.end_crit_edge, label %land.rhs

while.body.while.end_crit_edge:                   ; preds = %while.body
  %.pre = load i32, i32* inttoptr (i32 8 to i32*), align 8, !tbaa !14
  br label %while.end

while.end:                                        ; preds = %lor.rhs, %while.body.while.end_crit_edge
  %3 = phi i32 [ %.pre, %while.body.while.end_crit_edge ], [ %1, %lor.rhs ]
  %cur.0.lcssa = phi %struct.chunk* [ null, %while.body.while.end_crit_edge ], [ %cur.012, %lor.rhs ]
  %tobool.lcssa = phi i1 [ false, %while.body.while.end_crit_edge ], [ true, %lor.rhs ]
  tail call void @llvm.assume(i1 %tobool.lcssa)
  %add.i = add i32 %size, 7
  %and.i = and i32 %add.i, -8
  %length.i = getelementptr inbounds %struct.chunk, %struct.chunk* %cur.0.lcssa, i32 0, i32 2
  %sub.i = sub i32 %3, %and.i
  %cmp.i = icmp ugt i32 %sub.i, 23
  br i1 %cmp.i, label %if.then.i, label %shrink_chunk.exit

if.then.i:                                        ; preds = %while.end
  %add.ptr.i = getelementptr inbounds %struct.chunk, %struct.chunk* %cur.0.lcssa, i32 1
  %4 = bitcast %struct.chunk* %add.ptr.i to i8*
  %add.ptr1.i = getelementptr i8, i8* %4, i32 %and.i
  %next.i = getelementptr inbounds %struct.chunk, %struct.chunk* %cur.0.lcssa, i32 0, i32 0
  %5 = load %struct.chunk*, %struct.chunk** %next.i, align 4, !tbaa !15
  %next2.i = bitcast i8* %add.ptr1.i to %struct.chunk**
  store %struct.chunk* %5, %struct.chunk** %next2.i, align 4, !tbaa !15
  %cmp3.not.i = icmp eq %struct.chunk* %5, null
  br i1 %cmp3.not.i, label %if.end.i, label %if.then4.i

if.then4.i:                                       ; preds = %if.then.i
  %prev.i = getelementptr inbounds %struct.chunk, %struct.chunk* %5, i32 0, i32 1
  %6 = bitcast %struct.chunk** %prev.i to i8**
  store i8* %add.ptr1.i, i8** %6, align 4, !tbaa !16
  br label %if.end.i

if.end.i:                                         ; preds = %if.then4.i, %if.then.i
  %7 = bitcast %struct.chunk* %cur.0.lcssa to i8**
  store i8* %add.ptr1.i, i8** %7, align 4, !tbaa !15
  %prev7.i = getelementptr inbounds i8, i8* %add.ptr1.i, i32 4
  %8 = bitcast i8* %prev7.i to %struct.chunk**
  store %struct.chunk* %cur.0.lcssa, %struct.chunk** %8, align 4, !tbaa !16
  %allocated.i = getelementptr inbounds i8, i8* %add.ptr1.i, i32 12
  %9 = bitcast i8* %allocated.i to i32*
  store i32 0, i32* %9, align 4, !tbaa !10
  %sub10.i = add i32 %sub.i, -16
  %length11.i = getelementptr inbounds i8, i8* %add.ptr1.i, i32 8
  %10 = bitcast i8* %length11.i to i32*
  store i32 %sub10.i, i32* %10, align 4, !tbaa !14
  store i32 %and.i, i32* %length.i, align 4, !tbaa !14
  br label %shrink_chunk.exit

shrink_chunk.exit:                                ; preds = %while.end, %if.end.i
  %allocated3 = getelementptr inbounds %struct.chunk, %struct.chunk* %cur.0.lcssa, i32 0, i32 3
  store i32 1, i32* %allocated3, align 4, !tbaa !10
  %incdec.ptr = getelementptr inbounds %struct.chunk, %struct.chunk* %cur.0.lcssa, i32 1
  %11 = bitcast %struct.chunk* %incdec.ptr to i8*
  ret i8* %11
}

; Function Attrs: nounwind willreturn
declare void @llvm.assume(i1) #4

; Function Attrs: norecurse nounwind readonly
define internal i64 @vector_hash(%struct.vector* nocapture readonly %v) local_unnamed_addr #5 {
entry:
  %len2 = getelementptr inbounds %struct.vector, %struct.vector* %v, i32 0, i32 0
  %0 = load i32, i32* %len2, align 4, !tbaa !8
  %tobool.not8 = icmp eq i32 %0, 0
  br i1 %tobool.not8, label %while.end, label %while.body.lr.ph

while.body.lr.ph:                                 ; preds = %entry
  %arraydecay = getelementptr inbounds %struct.vector, %struct.vector* %v, i32 0, i32 2, i32 0
  %1 = load i8, i8* %arraydecay, align 1, !tbaa !7
  %conv = zext i8 %1 to i64
  %2 = add i32 %0, -1
  %3 = zext i32 %2 to i64
  %4 = add nuw nsw i64 %3, 1
  %5 = mul nuw nsw i64 %4, %conv
  br label %while.end

while.end:                                        ; preds = %while.body.lr.ph, %entry
  %hash.0.lcssa = phi i64 [ 0, %entry ], [ %5, %while.body.lr.ph ]
  ret i64 %hash.0.lcssa
}

; Function Attrs: norecurse nounwind readonly
define internal zeroext i1 @__memcmp(i8* nocapture readonly %left, i32 %left_len, i8* nocapture readonly %right, i32 %right_len) local_unnamed_addr #5 {
entry:
  %cmp.not = icmp eq i32 %left_len, %right_len
  br i1 %cmp.not, label %while.cond, label %return

while.cond:                                       ; preds = %entry, %while.body
  %left.addr.0 = phi i8* [ %incdec.ptr, %while.body ], [ %left, %entry ]
  %left_len.addr.0 = phi i32 [ %dec, %while.body ], [ %left_len, %entry ]
  %right.addr.0 = phi i8* [ %incdec.ptr1, %while.body ], [ %right, %entry ]
  %tobool.not = icmp eq i32 %left_len.addr.0, 0
  br i1 %tobool.not, label %return, label %while.body

while.body:                                       ; preds = %while.cond
  %dec = add i32 %left_len.addr.0, -1
  %incdec.ptr = getelementptr inbounds i8, i8* %left.addr.0, i32 1
  %0 = load i8, i8* %left.addr.0, align 1, !tbaa !7
  %incdec.ptr1 = getelementptr inbounds i8, i8* %right.addr.0, i32 1
  %1 = load i8, i8* %right.addr.0, align 1, !tbaa !7
  %cmp3.not = icmp eq i8 %0, %1
  br i1 %cmp3.not, label %while.cond, label %return

return:                                           ; preds = %while.cond, %while.body, %entry
  %retval.0 = phi i1 [ false, %entry ], [ true, %while.cond ], [ false, %while.body ]
  ret i1 %retval.0
}

; Function Attrs: nounwind
define internal %struct.vector* @concat(i8* nocapture readonly %left, i32 %left_len, i8* nocapture readonly %right, i32 %right_len) local_unnamed_addr #2 {
entry:
  %add = add i32 %right_len, %left_len
  %add1 = add i32 %add, 8
  %call = tail call i8* @__malloc(i32 %add1) #14
  %len = bitcast i8* %call to i32*
  store i32 %add, i32* %len, align 4, !tbaa !8
  %size = getelementptr inbounds i8, i8* %call, i32 4
  %0 = bitcast i8* %size to i32*
  store i32 %add, i32* %0, align 4, !tbaa !8
  %data2 = getelementptr inbounds i8, i8* %call, i32 8
  %tobool.not26 = icmp eq i32 %left_len, 0
  br i1 %tobool.not26, label %while.cond4.preheader, label %while.body

while.cond4.preheader:                            ; preds = %while.body, %entry
  %data.0.lcssa = phi i8* [ %data2, %entry ], [ %incdec.ptr3, %while.body ]
  %tobool6.not22 = icmp eq i32 %right_len, 0
  br i1 %tobool6.not22, label %while.end10, label %while.body7

while.body:                                       ; preds = %entry, %while.body
  %data.029 = phi i8* [ %incdec.ptr3, %while.body ], [ %data2, %entry ]
  %left.addr.028 = phi i8* [ %incdec.ptr, %while.body ], [ %left, %entry ]
  %left_len.addr.027 = phi i32 [ %dec, %while.body ], [ %left_len, %entry ]
  %dec = add i32 %left_len.addr.027, -1
  %incdec.ptr = getelementptr inbounds i8, i8* %left.addr.028, i32 1
  %1 = load i8, i8* %left.addr.028, align 1, !tbaa !7
  %incdec.ptr3 = getelementptr inbounds i8, i8* %data.029, i32 1
  store i8 %1, i8* %data.029, align 1, !tbaa !7
  %tobool.not = icmp eq i32 %dec, 0
  br i1 %tobool.not, label %while.cond4.preheader, label %while.body

while.body7:                                      ; preds = %while.cond4.preheader, %while.body7
  %data.125 = phi i8* [ %incdec.ptr9, %while.body7 ], [ %data.0.lcssa, %while.cond4.preheader ]
  %right_len.addr.024 = phi i32 [ %dec5, %while.body7 ], [ %right_len, %while.cond4.preheader ]
  %right.addr.023 = phi i8* [ %incdec.ptr8, %while.body7 ], [ %right, %while.cond4.preheader ]
  %dec5 = add i32 %right_len.addr.024, -1
  %incdec.ptr8 = getelementptr inbounds i8, i8* %right.addr.023, i32 1
  %2 = load i8, i8* %right.addr.023, align 1, !tbaa !7
  %incdec.ptr9 = getelementptr inbounds i8, i8* %data.125, i32 1
  store i8 %2, i8* %data.125, align 1, !tbaa !7
  %tobool6.not = icmp eq i32 %dec5, 0
  br i1 %tobool6.not, label %while.end10, label %while.body7

while.end10:                                      ; preds = %while.body7, %while.cond4.preheader
  %3 = bitcast i8* %call to %struct.vector*
  ret %struct.vector* %3
}

; Function Attrs: nofree nounwind
define internal void @__init_heap() local_unnamed_addr #6 {
entry:
  store %struct.chunk* null, %struct.chunk** inttoptr (i32 65540 to %struct.chunk**), align 4, !tbaa !16
  store %struct.chunk* null, %struct.chunk** inttoptr (i32 65536 to %struct.chunk**), align 65536, !tbaa !15
  store i32 0, i32* inttoptr (i32 65548 to i32*), align 4, !tbaa !10
  %0 = tail call i32 @llvm.wasm.memory.size.i32(i32 0)
  %mul = shl i32 %0, 16
  %sub1 = add i32 %mul, -65552
  store i32 %sub1, i32* inttoptr (i32 65544 to i32*), align 8, !tbaa !14
  ret void
}

; Function Attrs: nounwind readonly
declare i32 @llvm.wasm.memory.size.i32(i32) #7

; Function Attrs: nofree noinline norecurse nounwind
define internal void @__free(i8* %m) local_unnamed_addr #8 {
entry:
  %incdec.ptr = getelementptr inbounds i8, i8* %m, i32 -16
  %tobool.not = icmp eq i8* %m, null
  br i1 %tobool.not, label %if.end28, label %if.then

if.then:                                          ; preds = %entry
  %allocated = getelementptr inbounds i8, i8* %m, i32 -4
  %0 = bitcast i8* %allocated to i32*
  store i32 0, i32* %0, align 4, !tbaa !10
  %next1 = bitcast i8* %incdec.ptr to %struct.chunk**
  %1 = load %struct.chunk*, %struct.chunk** %next1, align 4, !tbaa !15
  %tobool2.not = icmp eq %struct.chunk* %1, null
  br i1 %tobool2.not, label %if.end13, label %land.lhs.true

land.lhs.true:                                    ; preds = %if.then
  %allocated3 = getelementptr inbounds %struct.chunk, %struct.chunk* %1, i32 0, i32 3
  %2 = load i32, i32* %allocated3, align 4, !tbaa !10
  %tobool4.not = icmp eq i32 %2, 0
  br i1 %tobool4.not, label %if.then5, label %if.end13

if.then5:                                         ; preds = %land.lhs.true
  %next6 = getelementptr inbounds %struct.chunk, %struct.chunk* %1, i32 0, i32 0
  %3 = load %struct.chunk*, %struct.chunk** %next6, align 4, !tbaa !15
  store %struct.chunk* %3, %struct.chunk** %next1, align 4, !tbaa !15
  %cmp.not = icmp eq %struct.chunk* %3, null
  br i1 %cmp.not, label %if.end, label %if.then8

if.then8:                                         ; preds = %if.then5
  %prev = getelementptr inbounds %struct.chunk, %struct.chunk* %3, i32 0, i32 1
  %4 = bitcast %struct.chunk** %prev to i8**
  store i8* %incdec.ptr, i8** %4, align 4, !tbaa !16
  br label %if.end

if.end:                                           ; preds = %if.then5, %if.then8
  %length = getelementptr inbounds %struct.chunk, %struct.chunk* %1, i32 0, i32 2
  %5 = load i32, i32* %length, align 4, !tbaa !14
  %add = add i32 %5, 16
  %length10 = getelementptr inbounds i8, i8* %m, i32 -8
  %6 = bitcast i8* %length10 to i32*
  %7 = load i32, i32* %6, align 4, !tbaa !14
  %add11 = add i32 %add, %7
  store i32 %add11, i32* %6, align 4, !tbaa !14
  br label %if.end13

if.end13:                                         ; preds = %land.lhs.true, %if.then, %if.end
  %next.0 = phi %struct.chunk* [ %1, %land.lhs.true ], [ %3, %if.end ], [ null, %if.then ]
  %prev15 = getelementptr inbounds i8, i8* %m, i32 -12
  %8 = bitcast i8* %prev15 to %struct.chunk**
  %9 = load %struct.chunk*, %struct.chunk** %8, align 4, !tbaa !16
  %tobool16.not = icmp eq %struct.chunk* %9, null
  br i1 %tobool16.not, label %if.end28, label %land.lhs.true17

land.lhs.true17:                                  ; preds = %if.end13
  %allocated18 = getelementptr inbounds %struct.chunk, %struct.chunk* %9, i32 0, i32 3
  %10 = load i32, i32* %allocated18, align 4, !tbaa !10
  %tobool19.not = icmp eq i32 %10, 0
  br i1 %tobool19.not, label %if.then20, label %if.end28

if.then20:                                        ; preds = %land.lhs.true17
  %next21 = getelementptr inbounds %struct.chunk, %struct.chunk* %9, i32 0, i32 0
  store %struct.chunk* %next.0, %struct.chunk** %next21, align 4, !tbaa !15
  %prev22 = getelementptr inbounds %struct.chunk, %struct.chunk* %next.0, i32 0, i32 1
  store %struct.chunk* %9, %struct.chunk** %prev22, align 4, !tbaa !16
  %length23 = getelementptr inbounds i8, i8* %m, i32 -8
  %11 = bitcast i8* %length23 to i32*
  %12 = load i32, i32* %11, align 4, !tbaa !14
  %add24 = add i32 %12, 16
  %length25 = getelementptr inbounds %struct.chunk, %struct.chunk* %9, i32 0, i32 2
  %13 = load i32, i32* %length25, align 4, !tbaa !14
  %add26 = add i32 %add24, %13
  store i32 %add26, i32* %length25, align 4, !tbaa !14
  br label %if.end28

if.end28:                                         ; preds = %if.then20, %if.end13, %land.lhs.true17, %entry
  ret void
}

; Function Attrs: nounwind
define internal i8* @__realloc(i8* %m, i32 %size) local_unnamed_addr #2 {
entry:
  %incdec.ptr = getelementptr inbounds i8, i8* %m, i32 -16
  %next1 = bitcast i8* %incdec.ptr to %struct.chunk**
  %0 = load %struct.chunk*, %struct.chunk** %next1, align 4, !tbaa !15
  %tobool.not = icmp eq %struct.chunk* %0, null
  br i1 %tobool.not, label %if.else, label %land.lhs.true

land.lhs.true:                                    ; preds = %entry
  %allocated = getelementptr inbounds %struct.chunk, %struct.chunk* %0, i32 0, i32 3
  %1 = load i32, i32* %allocated, align 4, !tbaa !10
  %tobool2.not = icmp eq i32 %1, 0
  br i1 %tobool2.not, label %land.lhs.true3, label %if.else

land.lhs.true3:                                   ; preds = %land.lhs.true
  %length = getelementptr inbounds i8, i8* %m, i32 -8
  %2 = bitcast i8* %length to i32*
  %3 = load i32, i32* %2, align 4, !tbaa !14
  %length4 = getelementptr inbounds %struct.chunk, %struct.chunk* %0, i32 0, i32 2
  %4 = load i32, i32* %length4, align 4, !tbaa !14
  %add = add i32 %3, 16
  %add5 = add i32 %add, %4
  %cmp.not = icmp ult i32 %add5, %size
  br i1 %cmp.not, label %if.else, label %if.then

if.then:                                          ; preds = %land.lhs.true3
  %next6 = getelementptr inbounds %struct.chunk, %struct.chunk* %0, i32 0, i32 0
  %5 = load %struct.chunk*, %struct.chunk** %next6, align 4, !tbaa !15
  store %struct.chunk* %5, %struct.chunk** %next1, align 4, !tbaa !15
  %prev = getelementptr inbounds %struct.chunk, %struct.chunk* %5, i32 0, i32 1
  %6 = bitcast %struct.chunk** %prev to i8**
  store i8* %incdec.ptr, i8** %6, align 4, !tbaa !16
  store i32 %add5, i32* %2, align 4, !tbaa !14
  %add.i = add i32 %size, 7
  %and.i = and i32 %add.i, -8
  %sub.i = sub i32 %add5, %and.i
  %cmp.i = icmp ugt i32 %sub.i, 23
  br i1 %cmp.i, label %if.then.i, label %cleanup

if.then.i:                                        ; preds = %if.then
  %add.ptr1.i = getelementptr i8, i8* %m, i32 %and.i
  %next2.i = bitcast i8* %add.ptr1.i to %struct.chunk**
  store %struct.chunk* %5, %struct.chunk** %next2.i, align 4, !tbaa !15
  %cmp3.not.i = icmp eq %struct.chunk* %5, null
  br i1 %cmp3.not.i, label %if.end.i, label %if.then4.i

if.then4.i:                                       ; preds = %if.then.i
  store i8* %add.ptr1.i, i8** %6, align 4, !tbaa !16
  br label %if.end.i

if.end.i:                                         ; preds = %if.then4.i, %if.then.i
  %7 = bitcast i8* %incdec.ptr to i8**
  store i8* %add.ptr1.i, i8** %7, align 4, !tbaa !15
  %prev7.i = getelementptr inbounds i8, i8* %add.ptr1.i, i32 4
  %8 = bitcast i8* %prev7.i to i8**
  store i8* %incdec.ptr, i8** %8, align 4, !tbaa !16
  %allocated.i = getelementptr inbounds i8, i8* %add.ptr1.i, i32 12
  %9 = bitcast i8* %allocated.i to i32*
  store i32 0, i32* %9, align 4, !tbaa !10
  %sub10.i = add i32 %sub.i, -16
  %length11.i = getelementptr inbounds i8, i8* %add.ptr1.i, i32 8
  %10 = bitcast i8* %length11.i to i32*
  store i32 %sub10.i, i32* %10, align 4, !tbaa !14
  store i32 %and.i, i32* %2, align 4, !tbaa !14
  br label %cleanup

if.else:                                          ; preds = %land.lhs.true3, %land.lhs.true, %entry
  %call = tail call i8* @__malloc(i32 %size) #15
  %div = lshr i32 %size, 3
  tail call void @__memcpy8(i8* nonnull %call, i8* nonnull %m, i32 %div) #14
  tail call void @__free(i8* nonnull %m) #15
  br label %cleanup

cleanup:                                          ; preds = %if.end.i, %if.then, %if.else
  %retval.0 = phi i8* [ %call, %if.else ], [ %m, %if.then ], [ %m, %if.end.i ]
  ret i8* %retval.0
}

; Function Attrs: nofree nounwind
define internal void @__mul32(i32* nocapture readonly %left, i32* nocapture readonly %right, i32* nocapture %out, i32 %len) local_unnamed_addr #6 {
entry:
  %0 = icmp slt i32 %len, 0
  %smin91 = select i1 %0, i32 %len, i32 0
  br label %while.cond

while.cond:                                       ; preds = %land.rhs, %entry
  %left_len.0 = phi i32 [ %len, %entry ], [ %sub, %land.rhs ]
  %cmp = icmp sgt i32 %left_len.0, 0
  br i1 %cmp, label %land.rhs, label %while.end

land.rhs:                                         ; preds = %while.cond
  %sub = add nsw i32 %left_len.0, -1
  %arrayidx = getelementptr inbounds i32, i32* %left, i32 %sub
  %1 = load i32, i32* %arrayidx, align 4, !tbaa !8
  %tobool.not = icmp eq i32 %1, 0
  br i1 %tobool.not, label %while.cond, label %while.end

while.end:                                        ; preds = %while.cond, %land.rhs
  %left_len.0.lcssa = phi i32 [ %smin91, %while.cond ], [ %left_len.0, %land.rhs ]
  br label %while.cond1

while.cond1:                                      ; preds = %land.rhs3, %while.end
  %right_len.0 = phi i32 [ %len, %while.end ], [ %sub4, %land.rhs3 ]
  %cmp2 = icmp sgt i32 %right_len.0, 0
  br i1 %cmp2, label %land.rhs3, label %while.end11

land.rhs3:                                        ; preds = %while.cond1
  %sub4 = add nsw i32 %right_len.0, -1
  %arrayidx5 = getelementptr inbounds i32, i32* %right, i32 %sub4
  %2 = load i32, i32* %arrayidx5, align 4, !tbaa !8
  %tobool6.not = icmp eq i32 %2, 0
  br i1 %tobool6.not, label %while.cond1, label %while.end11

while.end11:                                      ; preds = %while.cond1, %land.rhs3
  %right_len.0.lcssa = phi i32 [ %smin91, %while.cond1 ], [ %right_len.0, %land.rhs3 ]
  %cmp1285 = icmp sgt i32 %len, 0
  br i1 %cmp1285, label %for.body, label %for.cond.cleanup

for.cond.cleanup:                                 ; preds = %for.cond.cleanup25, %while.end11
  ret void

for.body:                                         ; preds = %while.end11, %for.cond.cleanup25
  %val1.090 = phi i64 [ %or, %for.cond.cleanup25 ], [ 0, %while.end11 ]
  %l.089 = phi i32 [ %inc38, %for.cond.cleanup25 ], [ 0, %while.end11 ]
  %left_start.088 = phi i32 [ %left_start.1, %for.cond.cleanup25 ], [ 0, %while.end11 ]
  %right_end.087 = phi i32 [ %spec.select76, %for.cond.cleanup25 ], [ 0, %while.end11 ]
  %right_start.086 = phi i32 [ %spec.select, %for.cond.cleanup25 ], [ 0, %while.end11 ]
  %cmp13.not = icmp sge i32 %l.089, %left_len.0.lcssa
  %inc = zext i1 %cmp13.not to i32
  %spec.select = add nuw nsw i32 %right_start.086, %inc
  %cmp14.not = icmp sge i32 %l.089, %right_len.0.lcssa
  %inc16 = zext i1 %cmp14.not to i32
  %left_start.1 = add nuw nsw i32 %left_start.088, %inc16
  %cmp18 = icmp slt i32 %right_end.087, %right_len.0.lcssa
  %inc20 = zext i1 %cmp18 to i32
  %spec.select76 = add nuw nsw i32 %right_end.087, %inc20
  %cmp24.not.not79 = icmp ugt i32 %spec.select76, %spec.select
  br i1 %cmp24.not.not79, label %for.body26, label %for.cond.cleanup25

for.cond.cleanup25.loopexit:                      ; preds = %for.body26
  %3 = extractvalue { i64, i1 } %6, 0
  br label %for.cond.cleanup25

for.cond.cleanup25:                               ; preds = %for.cond.cleanup25.loopexit, %for.body
  %carry.1.lcssa = phi i64 [ 0, %for.body ], [ %spec.select77, %for.cond.cleanup25.loopexit ]
  %val1.1.lcssa = phi i64 [ %val1.090, %for.body ], [ %3, %for.cond.cleanup25.loopexit ]
  %conv35 = trunc i64 %val1.1.lcssa to i32
  %arrayidx36 = getelementptr inbounds i32, i32* %out, i32 %l.089
  store i32 %conv35, i32* %arrayidx36, align 4, !tbaa !8
  %shr = lshr i64 %val1.1.lcssa, 32
  %or = or i64 %shr, %carry.1.lcssa
  %inc38 = add nuw nsw i32 %l.089, 1
  %exitcond.not = icmp eq i32 %inc38, %len
  br i1 %exitcond.not, label %for.cond.cleanup, label %for.body

for.body26:                                       ; preds = %for.body, %for.body26
  %r.083.in = phi i32 [ %r.083, %for.body26 ], [ %spec.select76, %for.body ]
  %val1.182 = phi i64 [ %8, %for.body26 ], [ %val1.090, %for.body ]
  %i.081 = phi i32 [ %inc30, %for.body26 ], [ 0, %for.body ]
  %carry.180 = phi i64 [ %spec.select77, %for.body26 ], [ 0, %for.body ]
  %r.083 = add nsw i32 %r.083.in, -1
  %add = add nuw nsw i32 %i.081, %left_start.1
  %arrayidx27 = getelementptr inbounds i32, i32* %left, i32 %add
  %4 = load i32, i32* %arrayidx27, align 4, !tbaa !8
  %conv = zext i32 %4 to i64
  %arrayidx28 = getelementptr inbounds i32, i32* %right, i32 %r.083
  %5 = load i32, i32* %arrayidx28, align 4, !tbaa !8
  %conv29 = zext i32 %5 to i64
  %mul = mul nuw i64 %conv29, %conv
  %inc30 = add nuw nsw i32 %i.081, 1
  %6 = tail call { i64, i1 } @llvm.uadd.with.overflow.i64(i64 %val1.182, i64 %mul)
  %7 = extractvalue { i64, i1 } %6, 1
  %8 = extractvalue { i64, i1 } %6, 0
  %add32 = add i64 %carry.180, 4294967296
  %spec.select77 = select i1 %7, i64 %add32, i64 %carry.180
  %cmp24.not.not = icmp sgt i32 %r.083, %spec.select
  br i1 %cmp24.not.not, label %for.body26, label %for.cond.cleanup25.loopexit
}

; Function Attrs: nounwind readnone speculatable willreturn
declare { i64, i1 } @llvm.uadd.with.overflow.i64(i64, i64) #9

; Function Attrs: norecurse nounwind readnone
define hidden i128 @__ashlti3(i128 %val, i32 %r) local_unnamed_addr #10 {
entry:
  %in.sroa.0.0.extract.trunc = trunc i128 %val to i64
  %in.sroa.7.0.extract.shift = lshr i128 %val, 64
  %in.sroa.7.0.extract.trunc = trunc i128 %in.sroa.7.0.extract.shift to i64
  %cmp = icmp eq i32 %r, 0
  br i1 %cmp, label %if.end17, label %if.else

if.else:                                          ; preds = %entry
  %and = and i32 %r, 64
  %tobool.not = icmp eq i32 %and, 0
  br i1 %tobool.not, label %if.else6, label %if.then3

if.then3:                                         ; preds = %if.else
  %and5 = and i32 %r, 63
  %sh_prom = zext i32 %and5 to i64
  %shl = shl i64 %in.sroa.0.0.extract.trunc, %sh_prom
  br label %if.end17

if.else6:                                         ; preds = %if.else
  %sh_prom8 = zext i32 %r to i64
  %shl9 = shl i64 %in.sroa.0.0.extract.trunc, %sh_prom8
  %shl13 = shl i64 %in.sroa.7.0.extract.trunc, %sh_prom8
  %sub = sub nsw i32 64, %r
  %sh_prom15 = zext i32 %sub to i64
  %shr = lshr i64 %in.sroa.0.0.extract.trunc, %sh_prom15
  %or = or i64 %shr, %shl13
  br label %if.end17

if.end17:                                         ; preds = %entry, %if.then3, %if.else6
  %result.sroa.6.0 = phi i64 [ %shl, %if.then3 ], [ %or, %if.else6 ], [ %in.sroa.7.0.extract.trunc, %entry ]
  %result.sroa.0.0 = phi i64 [ 0, %if.then3 ], [ %shl9, %if.else6 ], [ %in.sroa.0.0.extract.trunc, %entry ]
  %result.sroa.6.0.insert.ext = zext i64 %result.sroa.6.0 to i128
  %result.sroa.6.0.insert.shift = shl nuw i128 %result.sroa.6.0.insert.ext, 64
  %result.sroa.0.0.insert.ext = zext i64 %result.sroa.0.0 to i128
  %result.sroa.0.0.insert.insert = or i128 %result.sroa.6.0.insert.shift, %result.sroa.0.0.insert.ext
  ret i128 %result.sroa.0.0.insert.insert
}

; Function Attrs: norecurse nounwind readnone
define hidden i128 @__lshrti3(i128 %val, i32 %r) local_unnamed_addr #10 {
entry:
  %in.sroa.0.0.extract.trunc = trunc i128 %val to i64
  %in.sroa.5.0.extract.shift = lshr i128 %val, 64
  %in.sroa.5.0.extract.trunc = trunc i128 %in.sroa.5.0.extract.shift to i64
  %cmp = icmp eq i32 %r, 0
  br i1 %cmp, label %if.end17, label %if.else

if.else:                                          ; preds = %entry
  %and = and i32 %r, 64
  %tobool.not = icmp eq i32 %and, 0
  br i1 %tobool.not, label %if.else6, label %if.then3

if.then3:                                         ; preds = %if.else
  %and4 = and i32 %r, 63
  %sh_prom = zext i32 %and4 to i64
  %shr = lshr i64 %in.sroa.5.0.extract.trunc, %sh_prom
  br label %if.end17

if.else6:                                         ; preds = %if.else
  %sh_prom8 = zext i32 %r to i64
  %shr9 = lshr i64 %in.sroa.0.0.extract.trunc, %sh_prom8
  %sub = sub nsw i32 64, %r
  %sh_prom11 = zext i32 %sub to i64
  %shl = shl i64 %in.sroa.5.0.extract.trunc, %sh_prom11
  %or = or i64 %shl, %shr9
  %shr15 = lshr i64 %in.sroa.5.0.extract.trunc, %sh_prom8
  br label %if.end17

if.end17:                                         ; preds = %entry, %if.then3, %if.else6
  %result.sroa.6.0 = phi i64 [ 0, %if.then3 ], [ %shr15, %if.else6 ], [ %in.sroa.5.0.extract.trunc, %entry ]
  %result.sroa.0.0 = phi i64 [ %shr, %if.then3 ], [ %or, %if.else6 ], [ %in.sroa.0.0.extract.trunc, %entry ]
  %result.sroa.6.0.insert.ext = zext i64 %result.sroa.6.0 to i128
  %result.sroa.6.0.insert.shift = shl nuw i128 %result.sroa.6.0.insert.ext, 64
  %result.sroa.0.0.insert.ext = zext i64 %result.sroa.0.0 to i128
  %result.sroa.0.0.insert.insert = or i128 %result.sroa.6.0.insert.shift, %result.sroa.0.0.insert.ext
  ret i128 %result.sroa.0.0.insert.insert
}

; Function Attrs: norecurse nounwind readnone
define hidden i128 @__ashrti3(i128 %val, i32 %r) local_unnamed_addr #10 {
entry:
  %in.sroa.0.0.extract.trunc = trunc i128 %val to i64
  %in.sroa.5.0.extract.shift = lshr i128 %val, 64
  %in.sroa.5.0.extract.trunc = trunc i128 %in.sroa.5.0.extract.shift to i64
  %cmp = icmp eq i32 %r, 0
  br i1 %cmp, label %if.end19, label %if.else

if.else:                                          ; preds = %entry
  %and = and i32 %r, 64
  %tobool.not = icmp eq i32 %and, 0
  br i1 %tobool.not, label %if.else8, label %if.then3

if.then3:                                         ; preds = %if.else
  %shr = ashr i64 %in.sroa.5.0.extract.trunc, 63
  %and6 = and i32 %r, 63
  %sh_prom = zext i32 %and6 to i64
  %shr7 = ashr i64 %in.sroa.5.0.extract.trunc, %sh_prom
  br label %if.end19

if.else8:                                         ; preds = %if.else
  %sh_prom10 = zext i32 %r to i64
  %shr11 = lshr i64 %in.sroa.0.0.extract.trunc, %sh_prom10
  %sub = sub nsw i32 64, %r
  %sh_prom13 = zext i32 %sub to i64
  %shl = shl i64 %in.sroa.5.0.extract.trunc, %sh_prom13
  %or = or i64 %shl, %shr11
  %shr17 = ashr i64 %in.sroa.5.0.extract.trunc, %sh_prom10
  br label %if.end19

if.end19:                                         ; preds = %entry, %if.then3, %if.else8
  %result.sroa.6.0 = phi i64 [ %shr, %if.then3 ], [ %shr17, %if.else8 ], [ %in.sroa.5.0.extract.trunc, %entry ]
  %result.sroa.0.0 = phi i64 [ %shr7, %if.then3 ], [ %or, %if.else8 ], [ %in.sroa.0.0.extract.trunc, %entry ]
  %result.sroa.6.0.insert.ext = zext i64 %result.sroa.6.0 to i128
  %result.sroa.6.0.insert.shift = shl nuw i128 %result.sroa.6.0.insert.ext, 64
  %result.sroa.0.0.insert.ext = zext i64 %result.sroa.0.0 to i128
  %result.sroa.0.0.insert.insert = or i128 %result.sroa.6.0.insert.shift, %result.sroa.0.0.insert.ext
  ret i128 %result.sroa.0.0.insert.insert
}

; Function Attrs: norecurse nounwind readnone
define internal i32 @bits(i64 %v) local_unnamed_addr #10 {
entry:
  %tobool.not = icmp ult i64 %v, 4294967296
  %shl = shl i64 %v, 32
  %spec.select = select i1 %tobool.not, i64 %shl, i64 %v
  %spec.select47 = select i1 %tobool.not, i32 31, i32 63
  %tobool2.not = icmp ult i64 %spec.select, 281474976710656
  %sub4 = add nsw i32 %spec.select47, -16
  %shl5 = shl i64 %spec.select, 16
  %v.addr.1 = select i1 %tobool2.not, i64 %shl5, i64 %spec.select
  %h.1 = select i1 %tobool2.not, i32 %sub4, i32 %spec.select47
  %tobool8.not = icmp ult i64 %v.addr.1, 72057594037927936
  %sub10 = add nsw i32 %h.1, -8
  %shl11 = shl i64 %v.addr.1, 8
  %v.addr.2 = select i1 %tobool8.not, i64 %shl11, i64 %v.addr.1
  %h.2 = select i1 %tobool8.not, i32 %sub10, i32 %h.1
  %tobool14.not = icmp ult i64 %v.addr.2, 1152921504606846976
  %sub16 = add nsw i32 %h.2, -4
  %shl17 = shl i64 %v.addr.2, 4
  %v.addr.3 = select i1 %tobool14.not, i64 %shl17, i64 %v.addr.2
  %h.3 = select i1 %tobool14.not, i32 %sub16, i32 %h.2
  %tobool20.not = icmp ult i64 %v.addr.3, 4611686018427387904
  %sub22 = add nsw i32 %h.3, -2
  %shl23 = shl i64 %v.addr.3, 2
  %v.addr.4 = select i1 %tobool20.not, i64 %shl23, i64 %v.addr.3
  %h.4 = select i1 %tobool20.not, i32 %sub22, i32 %h.3
  %v.addr.4.lobit = ashr i64 %v.addr.4, 63
  %0 = trunc i64 %v.addr.4.lobit to i32
  %.not = xor i32 %0, -1
  %spec.select48 = add nsw i32 %h.4, %.not
  ret i32 %spec.select48
}

; Function Attrs: norecurse nounwind readnone
define internal i32 @bits128(i128 %v) local_unnamed_addr #10 {
entry:
  %shr = lshr i128 %v, 64
  %conv = trunc i128 %shr to i64
  %tobool.not = icmp eq i64 %conv, 0
  br i1 %tobool.not, label %if.else, label %if.then

if.then:                                          ; preds = %entry
  %tobool.not.i = icmp ult i64 %conv, 4294967296
  %shl.i33 = shl nuw nsw i128 %shr, 32
  %shl.i = trunc i128 %shl.i33 to i64
  %spec.select.i = select i1 %tobool.not.i, i64 %shl.i, i64 %conv
  %spec.select47.i = select i1 %tobool.not.i, i32 31, i32 63
  %tobool2.not.i = icmp ult i64 %spec.select.i, 281474976710656
  %sub4.i = add nsw i32 %spec.select47.i, -16
  %shl5.i = shl i64 %spec.select.i, 16
  %v.addr.1.i = select i1 %tobool2.not.i, i64 %shl5.i, i64 %spec.select.i
  %h.1.i = select i1 %tobool2.not.i, i32 %sub4.i, i32 %spec.select47.i
  %tobool8.not.i = icmp ult i64 %v.addr.1.i, 72057594037927936
  %sub10.i = add nsw i32 %h.1.i, -8
  %shl11.i = shl i64 %v.addr.1.i, 8
  %v.addr.2.i = select i1 %tobool8.not.i, i64 %shl11.i, i64 %v.addr.1.i
  %h.2.i = select i1 %tobool8.not.i, i32 %sub10.i, i32 %h.1.i
  %tobool14.not.i = icmp ult i64 %v.addr.2.i, 1152921504606846976
  %sub16.i = add nsw i32 %h.2.i, -4
  %shl17.i = shl i64 %v.addr.2.i, 4
  %v.addr.3.i = select i1 %tobool14.not.i, i64 %shl17.i, i64 %v.addr.2.i
  %h.3.i = select i1 %tobool14.not.i, i32 %sub16.i, i32 %h.2.i
  %tobool20.not.i = icmp ult i64 %v.addr.3.i, 4611686018427387904
  %sub22.i = add nsw i32 %h.3.i, -2
  %shl23.i = shl i64 %v.addr.3.i, 2
  %v.addr.4.i = select i1 %tobool20.not.i, i64 %shl23.i, i64 %v.addr.3.i
  %h.4.i = select i1 %tobool20.not.i, i32 %sub22.i, i32 %h.3.i
  %v.addr.4.lobit.i = ashr i64 %v.addr.4.i, 63
  %0 = trunc i64 %v.addr.4.lobit.i to i32
  %.not.i = xor i32 %0, -1
  %spec.select48.i = add nuw nsw i32 %h.4.i, 64
  %add = add i32 %spec.select48.i, %.not.i
  br label %cleanup

if.else:                                          ; preds = %entry
  %conv1 = trunc i128 %v to i64
  %tobool.not.i6 = icmp ult i64 %conv1, 4294967296
  %shl.i7 = shl i64 %conv1, 32
  %spec.select.i8 = select i1 %tobool.not.i6, i64 %shl.i7, i64 %conv1
  %spec.select47.i9 = select i1 %tobool.not.i6, i32 31, i32 63
  %tobool2.not.i10 = icmp ult i64 %spec.select.i8, 281474976710656
  %sub4.i11 = add nsw i32 %spec.select47.i9, -16
  %shl5.i12 = shl i64 %spec.select.i8, 16
  %v.addr.1.i13 = select i1 %tobool2.not.i10, i64 %shl5.i12, i64 %spec.select.i8
  %h.1.i14 = select i1 %tobool2.not.i10, i32 %sub4.i11, i32 %spec.select47.i9
  %tobool8.not.i15 = icmp ult i64 %v.addr.1.i13, 72057594037927936
  %sub10.i16 = add nsw i32 %h.1.i14, -8
  %shl11.i17 = shl i64 %v.addr.1.i13, 8
  %v.addr.2.i18 = select i1 %tobool8.not.i15, i64 %shl11.i17, i64 %v.addr.1.i13
  %h.2.i19 = select i1 %tobool8.not.i15, i32 %sub10.i16, i32 %h.1.i14
  %tobool14.not.i20 = icmp ult i64 %v.addr.2.i18, 1152921504606846976
  %sub16.i21 = add nsw i32 %h.2.i19, -4
  %shl17.i22 = shl i64 %v.addr.2.i18, 4
  %v.addr.3.i23 = select i1 %tobool14.not.i20, i64 %shl17.i22, i64 %v.addr.2.i18
  %h.3.i24 = select i1 %tobool14.not.i20, i32 %sub16.i21, i32 %h.2.i19
  %tobool20.not.i25 = icmp ult i64 %v.addr.3.i23, 4611686018427387904
  %sub22.i26 = add nsw i32 %h.3.i24, -2
  %shl23.i27 = shl i64 %v.addr.3.i23, 2
  %v.addr.4.i28 = select i1 %tobool20.not.i25, i64 %shl23.i27, i64 %v.addr.3.i23
  %h.4.i29 = select i1 %tobool20.not.i25, i32 %sub22.i26, i32 %h.3.i24
  %v.addr.4.lobit.i30 = ashr i64 %v.addr.4.i28, 63
  %1 = trunc i64 %v.addr.4.lobit.i30 to i32
  %.not.i31 = xor i32 %1, -1
  %spec.select48.i32 = add nsw i32 %h.4.i29, %.not.i31
  br label %cleanup

cleanup:                                          ; preds = %if.else, %if.then
  %retval.0 = phi i32 [ %add, %if.then ], [ %spec.select48.i32, %if.else ]
  ret i32 %retval.0
}

; Function Attrs: norecurse nounwind readnone
define internal i128 @shl128(i128 %val, i32 %r) local_unnamed_addr #10 {
entry:
  %cmp = icmp eq i32 %r, 0
  br i1 %cmp, label %return, label %if.else

if.else:                                          ; preds = %entry
  %and = and i32 %r, 64
  %tobool.not = icmp eq i32 %and, 0
  %conv7 = trunc i128 %val to i64
  br i1 %tobool.not, label %if.else5, label %if.then1

if.then1:                                         ; preds = %if.else
  %and2 = and i32 %r, 63
  %sh_prom = zext i32 %and2 to i64
  %shl = shl i64 %conv7, %sh_prom
  %conv3 = zext i64 %shl to i128
  %shl4 = shl nuw i128 %conv3, 64
  br label %return

if.else5:                                         ; preds = %if.else
  %shr = lshr i128 %val, 64
  %conv8 = trunc i128 %shr to i64
  %sh_prom10 = zext i32 %r to i64
  %shl11 = shl i64 %conv8, %sh_prom10
  %sub = sub nsw i32 64, %r
  %sh_prom12 = zext i32 %sub to i64
  %shr13 = lshr i64 %conv7, %sh_prom12
  %or = or i64 %shr13, %shl11
  %conv14 = zext i64 %or to i128
  %shl16 = shl i64 %conv7, %sh_prom10
  %conv17 = zext i64 %shl16 to i128
  %shl18 = shl nuw i128 %conv14, 64
  %or19 = or i128 %shl18, %conv17
  br label %return

return:                                           ; preds = %entry, %if.else5, %if.then1
  %retval.0 = phi i128 [ %shl4, %if.then1 ], [ %or19, %if.else5 ], [ %val, %entry ]
  ret i128 %retval.0
}

; Function Attrs: norecurse nounwind readnone
define internal i128 @shr128(i128 %val, i32 %r) local_unnamed_addr #10 {
entry:
  %cmp = icmp eq i32 %r, 0
  br i1 %cmp, label %return, label %if.else

if.else:                                          ; preds = %entry
  %and = and i32 %r, 64
  %tobool.not = icmp eq i32 %and, 0
  br i1 %tobool.not, label %if.else5, label %if.then1

if.then1:                                         ; preds = %if.else
  %shr = lshr i128 %val, 64
  %conv = trunc i128 %shr to i64
  %and2 = and i32 %r, 63
  %sh_prom = zext i32 %and2 to i64
  %shr3 = lshr i64 %conv, %sh_prom
  %conv4 = zext i64 %shr3 to i128
  br label %return

if.else5:                                         ; preds = %if.else
  %conv6 = trunc i128 %val to i64
  %shr8 = lshr i128 %val, 64
  %conv9 = trunc i128 %shr8 to i64
  %sh_prom10 = zext i32 %r to i64
  %shr11 = lshr i64 %conv6, %sh_prom10
  %sub = sub nsw i32 64, %r
  %sh_prom12 = zext i32 %sub to i64
  %shl = shl i64 %conv9, %sh_prom12
  %conv13 = zext i64 %shl to i128
  %conv14 = zext i64 %shr11 to i128
  %shl15 = shl nuw i128 %conv13, 64
  %or = or i128 %shl15, %conv14
  br label %return

return:                                           ; preds = %entry, %if.else5, %if.then1
  %retval.0 = phi i128 [ %conv4, %if.then1 ], [ %or, %if.else5 ], [ %val, %entry ]
  ret i128 %retval.0
}

; Function Attrs: nofree norecurse nounwind
define internal i32 @udivmod128(i128* nocapture readonly %pdividend, i128* nocapture readonly %pdivisor, i128* nocapture %remainder, i128* nocapture %quotient) local_unnamed_addr #1 {
entry:
  %0 = load i128, i128* %pdividend, align 16, !tbaa !17
  %1 = load i128, i128* %pdivisor, align 16, !tbaa !17
  switch i128 %1, label %if.end3 [
    i128 0, label %cleanup
    i128 1, label %if.then2
  ]

if.then2:                                         ; preds = %entry
  store i128 0, i128* %remainder, align 16, !tbaa !17
  store i128 %0, i128* %quotient, align 16, !tbaa !17
  br label %cleanup

if.end3:                                          ; preds = %entry
  %cmp4 = icmp eq i128 %1, %0
  br i1 %cmp4, label %if.then5, label %if.end6

if.then5:                                         ; preds = %if.end3
  store i128 0, i128* %remainder, align 16, !tbaa !17
  store i128 1, i128* %quotient, align 16, !tbaa !17
  br label %cleanup

if.end6:                                          ; preds = %if.end3
  %cmp7 = icmp eq i128 %0, 0
  %cmp8 = icmp ult i128 %0, %1
  %or.cond = or i1 %cmp7, %cmp8
  br i1 %or.cond, label %if.then9, label %if.end10

if.then9:                                         ; preds = %if.end6
  store i128 %0, i128* %remainder, align 16, !tbaa !17
  store i128 0, i128* %quotient, align 16, !tbaa !17
  br label %cleanup

if.end10:                                         ; preds = %if.end6
  %call = tail call i32 @bits128(i128 %0) #15
  %cmp1152 = icmp sgt i32 %call, -1
  br i1 %cmp1152, label %for.body.preheader, label %for.cond.cleanup

for.body.preheader:                               ; preds = %if.end10
  %add = add nuw nsw i32 %call, 1
  br label %for.body

for.cond.cleanup:                                 ; preds = %for.body, %if.end10
  %q.0.lcssa = phi i128 [ 0, %if.end10 ], [ %q.1, %for.body ]
  %r.0.lcssa = phi i128 [ 0, %if.end10 ], [ %r.2, %for.body ]
  store i128 %q.0.lcssa, i128* %quotient, align 16, !tbaa !17
  store i128 %r.0.lcssa, i128* %remainder, align 16, !tbaa !17
  br label %cleanup

for.body:                                         ; preds = %for.body.preheader, %for.body
  %x.055 = phi i32 [ %sub, %for.body ], [ %add, %for.body.preheader ]
  %r.054 = phi i128 [ %r.2, %for.body ], [ 0, %for.body.preheader ]
  %q.053 = phi i128 [ %q.1, %for.body ], [ 0, %for.body.preheader ]
  %shl = shl i128 %q.053, 1
  %shl12 = shl i128 %r.054, 1
  %sub = add nsw i32 %x.055, -1
  %sh_prom = zext i32 %sub to i128
  %2 = shl nuw i128 1, %sh_prom
  %3 = and i128 %2, %0
  %tobool.not = icmp ne i128 %3, 0
  %inc = zext i1 %tobool.not to i128
  %spec.select = or i128 %shl12, %inc
  %cmp15.not = icmp ult i128 %spec.select, %1
  %not.cmp15.not = xor i1 %cmp15.not, true
  %inc18 = zext i1 %not.cmp15.not to i128
  %q.1 = or i128 %shl, %inc18
  %sub17 = select i1 %cmp15.not, i128 0, i128 %1
  %r.2 = sub i128 %spec.select, %sub17
  %cmp11 = icmp sgt i32 %x.055, 1
  br i1 %cmp11, label %for.body, label %for.cond.cleanup

cleanup:                                          ; preds = %entry, %for.cond.cleanup, %if.then9, %if.then5, %if.then2
  %retval.0 = phi i32 [ 0, %if.then2 ], [ 0, %if.then5 ], [ 0, %if.then9 ], [ 0, %for.cond.cleanup ], [ 1, %entry ]
  ret i32 %retval.0
}

; Function Attrs: nofree norecurse nounwind
define internal i32 @sdivmod128(i128* nocapture %pdividend, i128* nocapture %pdivisor, i128* nocapture %remainder, i128* nocapture %quotient) local_unnamed_addr #1 {
entry:
  %0 = bitcast i128* %pdividend to i8*
  %arrayidx = getelementptr inbounds i8, i8* %0, i32 15
  %1 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %cmp = icmp slt i8 %1, 0
  br i1 %cmp, label %if.then, label %if.end

if.then:                                          ; preds = %entry
  %2 = load i128, i128* %pdividend, align 16, !tbaa !17
  %sub = sub i128 0, %2
  store i128 %sub, i128* %pdividend, align 16, !tbaa !17
  br label %if.end

if.end:                                           ; preds = %if.then, %entry
  %3 = bitcast i128* %pdivisor to i8*
  %arrayidx2 = getelementptr inbounds i8, i8* %3, i32 15
  %4 = load i8, i8* %arrayidx2, align 1, !tbaa !7
  %cmp4 = icmp slt i8 %4, 0
  %5 = load i128, i128* %pdivisor, align 16, !tbaa !17
  br i1 %cmp4, label %if.then8, label %if.end10

if.then8:                                         ; preds = %if.end
  %sub9 = sub i128 0, %5
  store i128 %sub9, i128* %pdivisor, align 16, !tbaa !17
  br label %if.end10

if.end10:                                         ; preds = %if.end, %if.then8
  %6 = phi i128 [ %sub9, %if.then8 ], [ %5, %if.end ]
  %7 = load i128, i128* %pdividend, align 16, !tbaa !17
  switch i128 %6, label %if.end3.i [
    i128 0, label %cleanup
    i128 1, label %if.then2.i
  ]

if.then2.i:                                       ; preds = %if.end10
  store i128 0, i128* %remainder, align 16, !tbaa !17
  store i128 %7, i128* %quotient, align 16, !tbaa !17
  br label %if.end13

if.end3.i:                                        ; preds = %if.end10
  %cmp4.i = icmp eq i128 %6, %7
  br i1 %cmp4.i, label %if.then5.i, label %if.end6.i

if.then5.i:                                       ; preds = %if.end3.i
  store i128 0, i128* %remainder, align 16, !tbaa !17
  store i128 1, i128* %quotient, align 16, !tbaa !17
  br label %if.end13

if.end6.i:                                        ; preds = %if.end3.i
  %cmp7.i = icmp eq i128 %7, 0
  %cmp8.i = icmp ult i128 %7, %6
  %or.cond.i = or i1 %cmp7.i, %cmp8.i
  br i1 %or.cond.i, label %if.then9.i, label %if.end10.i

if.then9.i:                                       ; preds = %if.end6.i
  store i128 %7, i128* %remainder, align 16, !tbaa !17
  store i128 0, i128* %quotient, align 16, !tbaa !17
  br label %if.end13

if.end10.i:                                       ; preds = %if.end6.i
  %call.i = tail call i32 @bits128(i128 %7) #14
  %cmp1152.i = icmp sgt i32 %call.i, -1
  br i1 %cmp1152.i, label %for.body.preheader.i, label %for.cond.cleanup.i

for.body.preheader.i:                             ; preds = %if.end10.i
  %add.i = add nuw nsw i32 %call.i, 1
  br label %for.body.i

for.cond.cleanup.i:                               ; preds = %for.body.i, %if.end10.i
  %q.0.lcssa.i = phi i128 [ 0, %if.end10.i ], [ %q.1.i, %for.body.i ]
  %r.0.lcssa.i = phi i128 [ 0, %if.end10.i ], [ %r.2.i, %for.body.i ]
  store i128 %q.0.lcssa.i, i128* %quotient, align 16, !tbaa !17
  store i128 %r.0.lcssa.i, i128* %remainder, align 16, !tbaa !17
  br label %if.end13

for.body.i:                                       ; preds = %for.body.i, %for.body.preheader.i
  %x.055.i = phi i32 [ %sub.i, %for.body.i ], [ %add.i, %for.body.preheader.i ]
  %r.054.i = phi i128 [ %r.2.i, %for.body.i ], [ 0, %for.body.preheader.i ]
  %q.053.i = phi i128 [ %q.1.i, %for.body.i ], [ 0, %for.body.preheader.i ]
  %shl.i = shl i128 %q.053.i, 1
  %shl12.i = shl i128 %r.054.i, 1
  %sub.i = add nsw i32 %x.055.i, -1
  %sh_prom.i = zext i32 %sub.i to i128
  %8 = shl nuw i128 1, %sh_prom.i
  %9 = and i128 %8, %7
  %tobool.not.i = icmp ne i128 %9, 0
  %inc.i = zext i1 %tobool.not.i to i128
  %spec.select.i = or i128 %shl12.i, %inc.i
  %cmp15.not.i = icmp ult i128 %spec.select.i, %6
  %not.cmp15.not.i = xor i1 %cmp15.not.i, true
  %inc18.i = zext i1 %not.cmp15.not.i to i128
  %q.1.i = or i128 %shl.i, %inc18.i
  %sub17.i = select i1 %cmp15.not.i, i128 0, i128 %6
  %r.2.i = sub i128 %spec.select.i, %sub17.i
  %cmp11.i = icmp sgt i32 %x.055.i, 1
  br i1 %cmp11.i, label %for.body.i, label %for.cond.cleanup.i

if.end13:                                         ; preds = %if.then2.i, %if.then5.i, %if.then9.i, %for.cond.cleanup.i
  %cmp18.not.unshifted = xor i8 %4, %1
  %cmp18.not = icmp sgt i8 %cmp18.not.unshifted, -1
  br i1 %cmp18.not, label %if.end22, label %if.then20

if.then20:                                        ; preds = %if.end13
  %10 = load i128, i128* %quotient, align 16, !tbaa !17
  %sub21 = sub i128 0, %10
  store i128 %sub21, i128* %quotient, align 16, !tbaa !17
  br label %if.end22

if.end22:                                         ; preds = %if.end13, %if.then20
  br i1 %cmp, label %if.then24, label %cleanup

if.then24:                                        ; preds = %if.end22
  %11 = load i128, i128* %remainder, align 16, !tbaa !17
  %sub25 = sub i128 0, %11
  store i128 %sub25, i128* %remainder, align 16, !tbaa !17
  br label %cleanup

cleanup:                                          ; preds = %if.end10, %if.end22, %if.then24
  %retval.0 = phi i32 [ 0, %if.then24 ], [ 0, %if.end22 ], [ 1, %if.end10 ]
  ret i32 %retval.0
}

; Function Attrs: norecurse nounwind readonly
define internal i32 @bits256(i256* nocapture readonly %value) local_unnamed_addr #5 {
entry:
  %0 = bitcast i256* %value to i64*
  %arrayidx = getelementptr inbounds i64, i64* %0, i32 3
  %1 = load i64, i64* %arrayidx, align 8, !tbaa !3
  %tobool.not = icmp eq i64 %1, 0
  br i1 %tobool.not, label %for.inc, label %cleanup

for.inc:                                          ; preds = %entry
  %arrayidx.1 = getelementptr inbounds i64, i64* %0, i32 2
  %2 = load i64, i64* %arrayidx.1, align 8, !tbaa !3
  %tobool.not.1 = icmp eq i64 %2, 0
  br i1 %tobool.not.1, label %for.inc.1, label %cleanup

cleanup:                                          ; preds = %for.inc.2, %for.inc.1, %for.inc, %entry
  %i.013.lcssa = phi i32 [ 192, %entry ], [ 128, %for.inc ], [ 64, %for.inc.1 ], [ 0, %for.inc.2 ]
  %.lcssa = phi i64 [ %1, %entry ], [ %2, %for.inc ], [ %5, %for.inc.1 ], [ %6, %for.inc.2 ]
  %tobool.not.i = icmp ult i64 %.lcssa, 4294967296
  %shl.i = shl i64 %.lcssa, 32
  %spec.select.i = select i1 %tobool.not.i, i64 %shl.i, i64 %.lcssa
  %spec.select47.i = select i1 %tobool.not.i, i32 31, i32 63
  %tobool2.not.i = icmp ult i64 %spec.select.i, 281474976710656
  %sub4.i = add nsw i32 %spec.select47.i, -16
  %shl5.i = shl i64 %spec.select.i, 16
  %v.addr.1.i = select i1 %tobool2.not.i, i64 %shl5.i, i64 %spec.select.i
  %h.1.i = select i1 %tobool2.not.i, i32 %sub4.i, i32 %spec.select47.i
  %tobool8.not.i = icmp ult i64 %v.addr.1.i, 72057594037927936
  %sub10.i = add nsw i32 %h.1.i, -8
  %shl11.i = shl i64 %v.addr.1.i, 8
  %v.addr.2.i = select i1 %tobool8.not.i, i64 %shl11.i, i64 %v.addr.1.i
  %h.2.i = select i1 %tobool8.not.i, i32 %sub10.i, i32 %h.1.i
  %tobool14.not.i = icmp ult i64 %v.addr.2.i, 1152921504606846976
  %sub16.i = add nsw i32 %h.2.i, -4
  %shl17.i = shl i64 %v.addr.2.i, 4
  %v.addr.3.i = select i1 %tobool14.not.i, i64 %shl17.i, i64 %v.addr.2.i
  %h.3.i = select i1 %tobool14.not.i, i32 %sub16.i, i32 %h.2.i
  %tobool20.not.i = icmp ult i64 %v.addr.3.i, 4611686018427387904
  %sub22.i = add nsw i32 %h.3.i, -2
  %shl23.i = shl i64 %v.addr.3.i, 2
  %v.addr.4.i = select i1 %tobool20.not.i, i64 %shl23.i, i64 %v.addr.3.i
  %h.4.i = select i1 %tobool20.not.i, i32 %sub22.i, i32 %h.3.i
  %v.addr.4.lobit.i = ashr i64 %v.addr.4.i, 63
  %3 = trunc i64 %v.addr.4.lobit.i to i32
  %.not.i = xor i32 %3, -1
  %spec.select48.i = add nuw nsw i32 %h.4.i, %i.013.lcssa
  %add = add i32 %spec.select48.i, %.not.i
  br label %.loopexit

.loopexit:                                        ; preds = %for.inc.2, %cleanup
  %4 = phi i32 [ %add, %cleanup ], [ 0, %for.inc.2 ]
  ret i32 %4

for.inc.1:                                        ; preds = %for.inc
  %arrayidx.2 = getelementptr inbounds i64, i64* %0, i32 1
  %5 = load i64, i64* %arrayidx.2, align 8, !tbaa !3
  %tobool.not.2 = icmp eq i64 %5, 0
  br i1 %tobool.not.2, label %for.inc.2, label %cleanup

for.inc.2:                                        ; preds = %for.inc.1
  %6 = load i64, i64* %0, align 8, !tbaa !3
  %tobool.not.3 = icmp eq i64 %6, 0
  br i1 %tobool.not.3, label %.loopexit, label %cleanup
}

; Function Attrs: nounwind
define internal i32 @udivmod256(i256* nocapture readonly %pdividend, i256* nocapture readonly %pdivisor, i256* nocapture %remainder, i256* nocapture %quotient) local_unnamed_addr #2 {
entry:
  %0 = load i256, i256* %pdividend, align 8, !tbaa !19
  %dividend.sroa.0.0.extract.trunc = trunc i256 %0 to i64
  %dividend.sroa.9.0.extract.shift = lshr i256 %0, 64
  %dividend.sroa.9.0.extract.trunc = trunc i256 %dividend.sroa.9.0.extract.shift to i64
  %dividend.sroa.11.0.extract.shift = lshr i256 %0, 128
  %dividend.sroa.11.0.extract.trunc = trunc i256 %dividend.sroa.11.0.extract.shift to i64
  %dividend.sroa.13.0.extract.shift = lshr i256 %0, 192
  %dividend.sroa.13.0.extract.trunc = trunc i256 %dividend.sroa.13.0.extract.shift to i64
  %1 = load i256, i256* %pdivisor, align 8, !tbaa !19
  %divisor.sroa.0.0.extract.trunc = trunc i256 %1 to i64
  %divisor.sroa.6.0.extract.shift = lshr i256 %1, 64
  %divisor.sroa.6.0.extract.trunc = trunc i256 %divisor.sroa.6.0.extract.shift to i64
  %divisor.sroa.8.0.extract.shift = lshr i256 %1, 128
  %divisor.sroa.8.0.extract.trunc = trunc i256 %divisor.sroa.8.0.extract.shift to i64
  %divisor.sroa.10.0.extract.shift = lshr i256 %1, 192
  %divisor.sroa.10.0.extract.trunc = trunc i256 %divisor.sroa.10.0.extract.shift to i64
  switch i256 %1, label %if.end3 [
    i256 0, label %cleanup
    i256 1, label %if.then2
  ]

if.then2:                                         ; preds = %entry
  store i256 0, i256* %remainder, align 8, !tbaa !19
  store i256 %0, i256* %quotient, align 8, !tbaa !19
  br label %cleanup

if.end3:                                          ; preds = %entry
  %cmp4 = icmp eq i256 %1, %0
  br i1 %cmp4, label %if.then5, label %if.end6

if.then5:                                         ; preds = %if.end3
  store i256 0, i256* %remainder, align 8, !tbaa !19
  store i256 1, i256* %quotient, align 8, !tbaa !19
  br label %cleanup

if.end6:                                          ; preds = %if.end3
  %cmp7 = icmp eq i256 %0, 0
  %cmp8 = icmp ult i256 %0, %1
  %or.cond = or i1 %cmp7, %cmp8
  br i1 %or.cond, label %if.then9, label %if.end10

if.then9:                                         ; preds = %if.end6
  store i256 %0, i256* %remainder, align 8, !tbaa !19
  store i256 0, i256* %quotient, align 8, !tbaa !19
  br label %cleanup

if.end10:                                         ; preds = %if.end6
  %tobool.not.i = icmp eq i64 %dividend.sroa.13.0.extract.trunc, 0
  br i1 %tobool.not.i, label %for.inc.i, label %cleanup.i

for.inc.i:                                        ; preds = %if.end10
  %tobool.not.1.i = icmp eq i64 %dividend.sroa.11.0.extract.trunc, 0
  br i1 %tobool.not.1.i, label %for.inc.1.i, label %cleanup.i

cleanup.i:                                        ; preds = %for.inc.2.i, %for.inc.1.i, %for.inc.i, %if.end10
  %i.013.lcssa.i = phi i32 [ 192, %if.end10 ], [ 128, %for.inc.i ], [ 64, %for.inc.1.i ], [ 0, %for.inc.2.i ]
  %.lcssa.i = phi i64 [ %dividend.sroa.13.0.extract.trunc, %if.end10 ], [ %dividend.sroa.11.0.extract.trunc, %for.inc.i ], [ %dividend.sroa.9.0.extract.trunc, %for.inc.1.i ], [ %dividend.sroa.0.0.extract.trunc, %for.inc.2.i ]
  %tobool.not.i.i = icmp ult i64 %.lcssa.i, 4294967296
  %shl.i.i = shl i64 %.lcssa.i, 32
  %spec.select.i.i = select i1 %tobool.not.i.i, i64 %shl.i.i, i64 %.lcssa.i
  %spec.select47.i.i = select i1 %tobool.not.i.i, i32 31, i32 63
  %tobool2.not.i.i = icmp ult i64 %spec.select.i.i, 281474976710656
  %sub4.i.i = add nsw i32 %spec.select47.i.i, -16
  %shl5.i.i = shl i64 %spec.select.i.i, 16
  %v.addr.1.i.i = select i1 %tobool2.not.i.i, i64 %shl5.i.i, i64 %spec.select.i.i
  %h.1.i.i = select i1 %tobool2.not.i.i, i32 %sub4.i.i, i32 %spec.select47.i.i
  %tobool8.not.i.i = icmp ult i64 %v.addr.1.i.i, 72057594037927936
  %sub10.i.i = add nsw i32 %h.1.i.i, -8
  %shl11.i.i = shl i64 %v.addr.1.i.i, 8
  %v.addr.2.i.i = select i1 %tobool8.not.i.i, i64 %shl11.i.i, i64 %v.addr.1.i.i
  %h.2.i.i = select i1 %tobool8.not.i.i, i32 %sub10.i.i, i32 %h.1.i.i
  %tobool14.not.i.i = icmp ult i64 %v.addr.2.i.i, 1152921504606846976
  %sub16.i.i = add nsw i32 %h.2.i.i, -4
  %shl17.i.i = shl i64 %v.addr.2.i.i, 4
  %v.addr.3.i.i = select i1 %tobool14.not.i.i, i64 %shl17.i.i, i64 %v.addr.2.i.i
  %h.3.i.i = select i1 %tobool14.not.i.i, i32 %sub16.i.i, i32 %h.2.i.i
  %tobool20.not.i.i = icmp ult i64 %v.addr.3.i.i, 4611686018427387904
  %sub22.i.i = add nsw i32 %h.3.i.i, -2
  %shl23.i.i = shl i64 %v.addr.3.i.i, 2
  %v.addr.4.i.i = select i1 %tobool20.not.i.i, i64 %shl23.i.i, i64 %v.addr.3.i.i
  %h.4.i.i = select i1 %tobool20.not.i.i, i32 %sub22.i.i, i32 %h.3.i.i
  %v.addr.4.lobit.i.i = ashr i64 %v.addr.4.i.i, 63
  %2 = trunc i64 %v.addr.4.lobit.i.i to i32
  %.not.i.i = xor i32 %2, -1
  %spec.select48.i.i = add nuw nsw i32 %h.4.i.i, %i.013.lcssa.i
  %add.i = add i32 %spec.select48.i.i, %.not.i.i
  br label %bits256.exit

for.inc.1.i:                                      ; preds = %for.inc.i
  %tobool.not.2.i = icmp eq i64 %dividend.sroa.9.0.extract.trunc, 0
  br i1 %tobool.not.2.i, label %for.inc.2.i, label %cleanup.i

for.inc.2.i:                                      ; preds = %for.inc.1.i
  %tobool.not.3.i = icmp eq i64 %dividend.sroa.0.0.extract.trunc, 0
  br i1 %tobool.not.3.i, label %bits256.exit, label %cleanup.i

bits256.exit:                                     ; preds = %cleanup.i, %for.inc.2.i
  %3 = phi i32 [ %add.i, %cleanup.i ], [ 0, %for.inc.2.i ]
  %tobool.not.i135 = icmp eq i64 %divisor.sroa.10.0.extract.trunc, 0
  br i1 %tobool.not.i135, label %for.inc.i138, label %cleanup.i169

for.inc.i138:                                     ; preds = %bits256.exit
  %tobool.not.1.i137 = icmp eq i64 %divisor.sroa.8.0.extract.trunc, 0
  br i1 %tobool.not.1.i137, label %for.inc.1.i172, label %cleanup.i169

cleanup.i169:                                     ; preds = %for.inc.2.i174, %for.inc.1.i172, %for.inc.i138, %bits256.exit
  %i.013.lcssa.i139 = phi i32 [ 192, %bits256.exit ], [ 128, %for.inc.i138 ], [ 64, %for.inc.1.i172 ], [ 0, %for.inc.2.i174 ]
  %.lcssa.i140 = phi i64 [ %divisor.sroa.10.0.extract.trunc, %bits256.exit ], [ %divisor.sroa.8.0.extract.trunc, %for.inc.i138 ], [ %divisor.sroa.6.0.extract.trunc, %for.inc.1.i172 ], [ %divisor.sroa.0.0.extract.trunc, %for.inc.2.i174 ]
  %tobool.not.i.i141 = icmp ult i64 %.lcssa.i140, 4294967296
  %shl.i.i142 = shl i64 %.lcssa.i140, 32
  %spec.select.i.i143 = select i1 %tobool.not.i.i141, i64 %shl.i.i142, i64 %.lcssa.i140
  %spec.select47.i.i144 = select i1 %tobool.not.i.i141, i32 31, i32 63
  %tobool2.not.i.i145 = icmp ult i64 %spec.select.i.i143, 281474976710656
  %sub4.i.i146 = add nsw i32 %spec.select47.i.i144, -16
  %shl5.i.i147 = shl i64 %spec.select.i.i143, 16
  %v.addr.1.i.i148 = select i1 %tobool2.not.i.i145, i64 %shl5.i.i147, i64 %spec.select.i.i143
  %h.1.i.i149 = select i1 %tobool2.not.i.i145, i32 %sub4.i.i146, i32 %spec.select47.i.i144
  %tobool8.not.i.i150 = icmp ult i64 %v.addr.1.i.i148, 72057594037927936
  %sub10.i.i151 = add nsw i32 %h.1.i.i149, -8
  %shl11.i.i152 = shl i64 %v.addr.1.i.i148, 8
  %v.addr.2.i.i153 = select i1 %tobool8.not.i.i150, i64 %shl11.i.i152, i64 %v.addr.1.i.i148
  %h.2.i.i154 = select i1 %tobool8.not.i.i150, i32 %sub10.i.i151, i32 %h.1.i.i149
  %tobool14.not.i.i155 = icmp ult i64 %v.addr.2.i.i153, 1152921504606846976
  %sub16.i.i156 = add nsw i32 %h.2.i.i154, -4
  %shl17.i.i157 = shl i64 %v.addr.2.i.i153, 4
  %v.addr.3.i.i158 = select i1 %tobool14.not.i.i155, i64 %shl17.i.i157, i64 %v.addr.2.i.i153
  %h.3.i.i159 = select i1 %tobool14.not.i.i155, i32 %sub16.i.i156, i32 %h.2.i.i154
  %tobool20.not.i.i160 = icmp ult i64 %v.addr.3.i.i158, 4611686018427387904
  %sub22.i.i161 = add nsw i32 %h.3.i.i159, -2
  %shl23.i.i162 = shl i64 %v.addr.3.i.i158, 2
  %v.addr.4.i.i163 = select i1 %tobool20.not.i.i160, i64 %shl23.i.i162, i64 %v.addr.3.i.i158
  %h.4.i.i164 = select i1 %tobool20.not.i.i160, i32 %sub22.i.i161, i32 %h.3.i.i159
  %v.addr.4.lobit.i.i165 = ashr i64 %v.addr.4.i.i163, 63
  %4 = trunc i64 %v.addr.4.lobit.i.i165 to i32
  %.not.i.i166 = xor i32 %4, -1
  %spec.select48.i.i167 = add nuw nsw i32 %h.4.i.i164, %i.013.lcssa.i139
  %add.i168 = add i32 %spec.select48.i.i167, %.not.i.i166
  br label %bits256.exit175

for.inc.1.i172:                                   ; preds = %for.inc.i138
  %tobool.not.2.i171 = icmp eq i64 %divisor.sroa.6.0.extract.trunc, 0
  br i1 %tobool.not.2.i171, label %for.inc.2.i174, label %cleanup.i169

for.inc.2.i174:                                   ; preds = %for.inc.1.i172
  %tobool.not.3.i173 = icmp eq i64 %divisor.sroa.0.0.extract.trunc, 0
  br i1 %tobool.not.3.i173, label %bits256.exit175, label %cleanup.i169

bits256.exit175:                                  ; preds = %cleanup.i169, %for.inc.2.i174
  %5 = phi i32 [ %add.i168, %cleanup.i169 ], [ 0, %for.inc.2.i174 ]
  %sub = sub nsw i32 %3, %5
  %sh_prom = zext i32 %sub to i256
  %shl = shl i256 %1, %sh_prom
  br i1 %tobool.not.i, label %for.inc.i96, label %cleanup.i127

for.inc.i96:                                      ; preds = %bits256.exit175
  %tobool.not.1.i95 = icmp eq i64 %dividend.sroa.11.0.extract.trunc, 0
  br i1 %tobool.not.1.i95, label %for.inc.1.i130, label %cleanup.i127

cleanup.i127:                                     ; preds = %for.inc.2.i132, %for.inc.1.i130, %for.inc.i96, %bits256.exit175
  %i.013.lcssa.i97 = phi i32 [ 192, %bits256.exit175 ], [ 128, %for.inc.i96 ], [ 64, %for.inc.1.i130 ], [ 0, %for.inc.2.i132 ]
  %.lcssa.i98 = phi i64 [ %dividend.sroa.13.0.extract.trunc, %bits256.exit175 ], [ %dividend.sroa.11.0.extract.trunc, %for.inc.i96 ], [ %dividend.sroa.9.0.extract.trunc, %for.inc.1.i130 ], [ %dividend.sroa.0.0.extract.trunc, %for.inc.2.i132 ]
  %tobool.not.i.i99 = icmp ult i64 %.lcssa.i98, 4294967296
  %shl.i.i100 = shl i64 %.lcssa.i98, 32
  %spec.select.i.i101 = select i1 %tobool.not.i.i99, i64 %shl.i.i100, i64 %.lcssa.i98
  %spec.select47.i.i102 = select i1 %tobool.not.i.i99, i32 31, i32 63
  %tobool2.not.i.i103 = icmp ult i64 %spec.select.i.i101, 281474976710656
  %sub4.i.i104 = add nsw i32 %spec.select47.i.i102, -16
  %shl5.i.i105 = shl i64 %spec.select.i.i101, 16
  %v.addr.1.i.i106 = select i1 %tobool2.not.i.i103, i64 %shl5.i.i105, i64 %spec.select.i.i101
  %h.1.i.i107 = select i1 %tobool2.not.i.i103, i32 %sub4.i.i104, i32 %spec.select47.i.i102
  %tobool8.not.i.i108 = icmp ult i64 %v.addr.1.i.i106, 72057594037927936
  %sub10.i.i109 = add nsw i32 %h.1.i.i107, -8
  %shl11.i.i110 = shl i64 %v.addr.1.i.i106, 8
  %v.addr.2.i.i111 = select i1 %tobool8.not.i.i108, i64 %shl11.i.i110, i64 %v.addr.1.i.i106
  %h.2.i.i112 = select i1 %tobool8.not.i.i108, i32 %sub10.i.i109, i32 %h.1.i.i107
  %tobool14.not.i.i113 = icmp ult i64 %v.addr.2.i.i111, 1152921504606846976
  %sub16.i.i114 = add nsw i32 %h.2.i.i112, -4
  %shl17.i.i115 = shl i64 %v.addr.2.i.i111, 4
  %v.addr.3.i.i116 = select i1 %tobool14.not.i.i113, i64 %shl17.i.i115, i64 %v.addr.2.i.i111
  %h.3.i.i117 = select i1 %tobool14.not.i.i113, i32 %sub16.i.i114, i32 %h.2.i.i112
  %tobool20.not.i.i118 = icmp ult i64 %v.addr.3.i.i116, 4611686018427387904
  %sub22.i.i119 = add nsw i32 %h.3.i.i117, -2
  %shl23.i.i120 = shl i64 %v.addr.3.i.i116, 2
  %v.addr.4.i.i121 = select i1 %tobool20.not.i.i118, i64 %shl23.i.i120, i64 %v.addr.3.i.i116
  %h.4.i.i122 = select i1 %tobool20.not.i.i118, i32 %sub22.i.i119, i32 %h.3.i.i117
  %v.addr.4.lobit.i.i123 = ashr i64 %v.addr.4.i.i121, 63
  %6 = trunc i64 %v.addr.4.lobit.i.i123 to i32
  %.not.i.i124 = xor i32 %6, -1
  %spec.select48.i.i125 = add nuw nsw i32 %h.4.i.i122, %i.013.lcssa.i97
  %add.i126 = add i32 %spec.select48.i.i125, %.not.i.i124
  br label %bits256.exit133

for.inc.1.i130:                                   ; preds = %for.inc.i96
  %tobool.not.2.i129 = icmp eq i64 %dividend.sroa.9.0.extract.trunc, 0
  br i1 %tobool.not.2.i129, label %for.inc.2.i132, label %cleanup.i127

for.inc.2.i132:                                   ; preds = %for.inc.1.i130
  %tobool.not.3.i131 = icmp eq i64 %dividend.sroa.0.0.extract.trunc, 0
  br i1 %tobool.not.3.i131, label %bits256.exit133, label %cleanup.i127

bits256.exit133:                                  ; preds = %cleanup.i127, %for.inc.2.i132
  %7 = phi i32 [ %add.i126, %cleanup.i127 ], [ 0, %for.inc.2.i132 ]
  br i1 %tobool.not.i135, label %for.inc.i54, label %cleanup.i85

for.inc.i54:                                      ; preds = %bits256.exit133
  %tobool.not.1.i53 = icmp eq i64 %divisor.sroa.8.0.extract.trunc, 0
  br i1 %tobool.not.1.i53, label %for.inc.1.i88, label %cleanup.i85

cleanup.i85:                                      ; preds = %for.inc.2.i90, %for.inc.1.i88, %for.inc.i54, %bits256.exit133
  %i.013.lcssa.i55 = phi i32 [ 192, %bits256.exit133 ], [ 128, %for.inc.i54 ], [ 64, %for.inc.1.i88 ], [ 0, %for.inc.2.i90 ]
  %.lcssa.i56 = phi i64 [ %divisor.sroa.10.0.extract.trunc, %bits256.exit133 ], [ %divisor.sroa.8.0.extract.trunc, %for.inc.i54 ], [ %divisor.sroa.6.0.extract.trunc, %for.inc.1.i88 ], [ %divisor.sroa.0.0.extract.trunc, %for.inc.2.i90 ]
  %tobool.not.i.i57 = icmp ult i64 %.lcssa.i56, 4294967296
  %shl.i.i58 = shl i64 %.lcssa.i56, 32
  %spec.select.i.i59 = select i1 %tobool.not.i.i57, i64 %shl.i.i58, i64 %.lcssa.i56
  %spec.select47.i.i60 = select i1 %tobool.not.i.i57, i32 31, i32 63
  %tobool2.not.i.i61 = icmp ult i64 %spec.select.i.i59, 281474976710656
  %sub4.i.i62 = add nsw i32 %spec.select47.i.i60, -16
  %shl5.i.i63 = shl i64 %spec.select.i.i59, 16
  %v.addr.1.i.i64 = select i1 %tobool2.not.i.i61, i64 %shl5.i.i63, i64 %spec.select.i.i59
  %h.1.i.i65 = select i1 %tobool2.not.i.i61, i32 %sub4.i.i62, i32 %spec.select47.i.i60
  %tobool8.not.i.i66 = icmp ult i64 %v.addr.1.i.i64, 72057594037927936
  %sub10.i.i67 = add nsw i32 %h.1.i.i65, -8
  %shl11.i.i68 = shl i64 %v.addr.1.i.i64, 8
  %v.addr.2.i.i69 = select i1 %tobool8.not.i.i66, i64 %shl11.i.i68, i64 %v.addr.1.i.i64
  %h.2.i.i70 = select i1 %tobool8.not.i.i66, i32 %sub10.i.i67, i32 %h.1.i.i65
  %tobool14.not.i.i71 = icmp ult i64 %v.addr.2.i.i69, 1152921504606846976
  %sub16.i.i72 = add nsw i32 %h.2.i.i70, -4
  %shl17.i.i73 = shl i64 %v.addr.2.i.i69, 4
  %v.addr.3.i.i74 = select i1 %tobool14.not.i.i71, i64 %shl17.i.i73, i64 %v.addr.2.i.i69
  %h.3.i.i75 = select i1 %tobool14.not.i.i71, i32 %sub16.i.i72, i32 %h.2.i.i70
  %tobool20.not.i.i76 = icmp ult i64 %v.addr.3.i.i74, 4611686018427387904
  %sub22.i.i77 = add nsw i32 %h.3.i.i75, -2
  %shl23.i.i78 = shl i64 %v.addr.3.i.i74, 2
  %v.addr.4.i.i79 = select i1 %tobool20.not.i.i76, i64 %shl23.i.i78, i64 %v.addr.3.i.i74
  %h.4.i.i80 = select i1 %tobool20.not.i.i76, i32 %sub22.i.i77, i32 %h.3.i.i75
  %v.addr.4.lobit.i.i81 = ashr i64 %v.addr.4.i.i79, 63
  %8 = trunc i64 %v.addr.4.lobit.i.i81 to i32
  %.not.i.i82 = xor i32 %8, -1
  %spec.select48.i.i83 = add nuw nsw i32 %h.4.i.i80, %i.013.lcssa.i55
  %add.i84 = add i32 %spec.select48.i.i83, %.not.i.i82
  br label %while.body.preheader

for.inc.1.i88:                                    ; preds = %for.inc.i54
  %tobool.not.2.i87 = icmp eq i64 %divisor.sroa.6.0.extract.trunc, 0
  br i1 %tobool.not.2.i87, label %for.inc.2.i90, label %cleanup.i85

for.inc.2.i90:                                    ; preds = %for.inc.1.i88
  %tobool.not.3.i89 = icmp eq i64 %divisor.sroa.0.0.extract.trunc, 0
  br i1 %tobool.not.3.i89, label %while.body.preheader, label %cleanup.i85

while.body.preheader:                             ; preds = %for.inc.2.i90, %cleanup.i85
  %9 = phi i32 [ %add.i84, %cleanup.i85 ], [ 0, %for.inc.2.i90 ]
  %cmp17 = icmp ugt i256 %shl, %0
  %shr = zext i1 %cmp17 to i256
  %sub14 = sub nsw i32 %7, %9
  %sh_prom15 = zext i32 %sub14 to i256
  %shl16 = shl nuw i256 1, %sh_prom15
  %adder.0 = lshr i256 %shl16, %shr
  %copyd.0 = lshr i256 %shl, %shr
  br label %while.body

while.body:                                       ; preds = %while.body.preheader, %while.body
  %adder.1255 = phi i256 [ %shr27, %while.body ], [ %adder.0, %while.body.preheader ]
  %copyd.1254 = phi i256 [ %shr26, %while.body ], [ %copyd.0, %while.body.preheader ]
  %r.0253 = phi i256 [ %r.1, %while.body ], [ %0, %while.body.preheader ]
  %q.0252 = phi i256 [ %q.1, %while.body ], [ 0, %while.body.preheader ]
  %cmp22.not = icmp ult i256 %r.0253, %copyd.1254
  %or = select i1 %cmp22.not, i256 0, i256 %adder.1255
  %q.1 = or i256 %or, %q.0252
  %sub24 = select i1 %cmp22.not, i256 0, i256 %copyd.1254
  %r.1 = sub i256 %r.0253, %sub24
  %shr26 = lshr i256 %copyd.1254, 1
  %shr27 = lshr i256 %adder.1255, 1
  %cmp21.not = icmp ult i256 %r.1, %1
  br i1 %cmp21.not, label %while.end, label %while.body

while.end:                                        ; preds = %while.body
  store i256 %q.1, i256* %quotient, align 8, !tbaa !19
  store i256 %r.1, i256* %remainder, align 8, !tbaa !19
  br label %cleanup

cleanup:                                          ; preds = %entry, %while.end, %if.then9, %if.then5, %if.then2
  %retval.0 = phi i32 [ 0, %if.then2 ], [ 0, %if.then5 ], [ 0, %if.then9 ], [ 0, %while.end ], [ 1, %entry ]
  ret i32 %retval.0
}

; Function Attrs: nounwind
define internal i32 @sdivmod256(i256* nocapture %pdividend, i256* nocapture %pdivisor, i256* nocapture %remainder, i256* nocapture %quotient) local_unnamed_addr #2 {
entry:
  %0 = bitcast i256* %pdividend to i8*
  %arrayidx = getelementptr inbounds i8, i8* %0, i32 31
  %1 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %cmp = icmp slt i8 %1, 0
  br i1 %cmp, label %if.then, label %if.end

if.then:                                          ; preds = %entry
  %2 = load i256, i256* %pdividend, align 8, !tbaa !19
  %sub = sub i256 0, %2
  store i256 %sub, i256* %pdividend, align 8, !tbaa !19
  br label %if.end

if.end:                                           ; preds = %if.then, %entry
  %3 = bitcast i256* %pdivisor to i8*
  %arrayidx2 = getelementptr inbounds i8, i8* %3, i32 31
  %4 = load i8, i8* %arrayidx2, align 1, !tbaa !7
  %cmp4 = icmp slt i8 %4, 0
  br i1 %cmp4, label %if.then8, label %if.end10

if.then8:                                         ; preds = %if.end
  %5 = load i256, i256* %pdivisor, align 8, !tbaa !19
  %sub9 = sub i256 0, %5
  store i256 %sub9, i256* %pdivisor, align 8, !tbaa !19
  br label %if.end10

if.end10:                                         ; preds = %if.then8, %if.end
  %call = tail call i32 @udivmod256(i256* nonnull %pdividend, i256* nonnull %pdivisor, i256* %remainder, i256* %quotient) #15
  %tobool11.not = icmp eq i32 %call, 0
  br i1 %tobool11.not, label %if.end13, label %cleanup

if.end13:                                         ; preds = %if.end10
  %cmp18.not.unshifted = xor i8 %4, %1
  %cmp18.not = icmp sgt i8 %cmp18.not.unshifted, -1
  br i1 %cmp18.not, label %if.end22, label %if.then20

if.then20:                                        ; preds = %if.end13
  %6 = load i256, i256* %quotient, align 8, !tbaa !19
  %sub21 = sub i256 0, %6
  store i256 %sub21, i256* %quotient, align 8, !tbaa !19
  br label %if.end22

if.end22:                                         ; preds = %if.end13, %if.then20
  br i1 %cmp, label %if.then24, label %cleanup

if.then24:                                        ; preds = %if.end22
  %7 = load i256, i256* %remainder, align 8, !tbaa !19
  %sub25 = sub i256 0, %7
  store i256 %sub25, i256* %remainder, align 8, !tbaa !19
  br label %cleanup

cleanup:                                          ; preds = %if.end22, %if.then24, %if.end10
  %retval.0 = phi i32 [ 1, %if.end10 ], [ 0, %if.then24 ], [ 0, %if.end22 ]
  ret i32 %retval.0
}

; Function Attrs: norecurse nounwind readonly
define internal i32 @bits512(i512* nocapture readonly %value) local_unnamed_addr #5 {
entry:
  %0 = bitcast i512* %value to i64*
  %arrayidx = getelementptr inbounds i64, i64* %0, i32 7
  %1 = load i64, i64* %arrayidx, align 8, !tbaa !3
  %tobool.not = icmp eq i64 %1, 0
  br i1 %tobool.not, label %for.inc, label %cleanup

for.inc:                                          ; preds = %entry
  %arrayidx.1 = getelementptr inbounds i64, i64* %0, i32 6
  %2 = load i64, i64* %arrayidx.1, align 8, !tbaa !3
  %tobool.not.1 = icmp eq i64 %2, 0
  br i1 %tobool.not.1, label %for.inc.1, label %cleanup

cleanup:                                          ; preds = %for.inc.6, %for.inc.5, %for.inc.4, %for.inc.3, %for.inc.2, %for.inc.1, %for.inc, %entry
  %i.013.lcssa = phi i32 [ 448, %entry ], [ 384, %for.inc ], [ 320, %for.inc.1 ], [ 256, %for.inc.2 ], [ 192, %for.inc.3 ], [ 128, %for.inc.4 ], [ 64, %for.inc.5 ], [ 0, %for.inc.6 ]
  %.lcssa = phi i64 [ %1, %entry ], [ %2, %for.inc ], [ %5, %for.inc.1 ], [ %6, %for.inc.2 ], [ %7, %for.inc.3 ], [ %8, %for.inc.4 ], [ %9, %for.inc.5 ], [ %10, %for.inc.6 ]
  %tobool.not.i = icmp ult i64 %.lcssa, 4294967296
  %shl.i = shl i64 %.lcssa, 32
  %spec.select.i = select i1 %tobool.not.i, i64 %shl.i, i64 %.lcssa
  %spec.select47.i = select i1 %tobool.not.i, i32 31, i32 63
  %tobool2.not.i = icmp ult i64 %spec.select.i, 281474976710656
  %sub4.i = add nsw i32 %spec.select47.i, -16
  %shl5.i = shl i64 %spec.select.i, 16
  %v.addr.1.i = select i1 %tobool2.not.i, i64 %shl5.i, i64 %spec.select.i
  %h.1.i = select i1 %tobool2.not.i, i32 %sub4.i, i32 %spec.select47.i
  %tobool8.not.i = icmp ult i64 %v.addr.1.i, 72057594037927936
  %sub10.i = add nsw i32 %h.1.i, -8
  %shl11.i = shl i64 %v.addr.1.i, 8
  %v.addr.2.i = select i1 %tobool8.not.i, i64 %shl11.i, i64 %v.addr.1.i
  %h.2.i = select i1 %tobool8.not.i, i32 %sub10.i, i32 %h.1.i
  %tobool14.not.i = icmp ult i64 %v.addr.2.i, 1152921504606846976
  %sub16.i = add nsw i32 %h.2.i, -4
  %shl17.i = shl i64 %v.addr.2.i, 4
  %v.addr.3.i = select i1 %tobool14.not.i, i64 %shl17.i, i64 %v.addr.2.i
  %h.3.i = select i1 %tobool14.not.i, i32 %sub16.i, i32 %h.2.i
  %tobool20.not.i = icmp ult i64 %v.addr.3.i, 4611686018427387904
  %sub22.i = add nsw i32 %h.3.i, -2
  %shl23.i = shl i64 %v.addr.3.i, 2
  %v.addr.4.i = select i1 %tobool20.not.i, i64 %shl23.i, i64 %v.addr.3.i
  %h.4.i = select i1 %tobool20.not.i, i32 %sub22.i, i32 %h.3.i
  %v.addr.4.lobit.i = ashr i64 %v.addr.4.i, 63
  %3 = trunc i64 %v.addr.4.lobit.i to i32
  %.not.i = xor i32 %3, -1
  %spec.select48.i = add nuw nsw i32 %h.4.i, %i.013.lcssa
  %add = add i32 %spec.select48.i, %.not.i
  br label %.loopexit

.loopexit:                                        ; preds = %for.inc.6, %cleanup
  %4 = phi i32 [ %add, %cleanup ], [ 0, %for.inc.6 ]
  ret i32 %4

for.inc.1:                                        ; preds = %for.inc
  %arrayidx.2 = getelementptr inbounds i64, i64* %0, i32 5
  %5 = load i64, i64* %arrayidx.2, align 8, !tbaa !3
  %tobool.not.2 = icmp eq i64 %5, 0
  br i1 %tobool.not.2, label %for.inc.2, label %cleanup

for.inc.2:                                        ; preds = %for.inc.1
  %arrayidx.3 = getelementptr inbounds i64, i64* %0, i32 4
  %6 = load i64, i64* %arrayidx.3, align 8, !tbaa !3
  %tobool.not.3 = icmp eq i64 %6, 0
  br i1 %tobool.not.3, label %for.inc.3, label %cleanup

for.inc.3:                                        ; preds = %for.inc.2
  %arrayidx.4 = getelementptr inbounds i64, i64* %0, i32 3
  %7 = load i64, i64* %arrayidx.4, align 8, !tbaa !3
  %tobool.not.4 = icmp eq i64 %7, 0
  br i1 %tobool.not.4, label %for.inc.4, label %cleanup

for.inc.4:                                        ; preds = %for.inc.3
  %arrayidx.5 = getelementptr inbounds i64, i64* %0, i32 2
  %8 = load i64, i64* %arrayidx.5, align 8, !tbaa !3
  %tobool.not.5 = icmp eq i64 %8, 0
  br i1 %tobool.not.5, label %for.inc.5, label %cleanup

for.inc.5:                                        ; preds = %for.inc.4
  %arrayidx.6 = getelementptr inbounds i64, i64* %0, i32 1
  %9 = load i64, i64* %arrayidx.6, align 8, !tbaa !3
  %tobool.not.6 = icmp eq i64 %9, 0
  br i1 %tobool.not.6, label %for.inc.6, label %cleanup

for.inc.6:                                        ; preds = %for.inc.5
  %10 = load i64, i64* %0, align 8, !tbaa !3
  %tobool.not.7 = icmp eq i64 %10, 0
  br i1 %tobool.not.7, label %.loopexit, label %cleanup
}

; Function Attrs: nounwind
define internal i32 @udivmod512(i512* nocapture readonly %pdividend, i512* nocapture readonly %pdivisor, i512* nocapture %remainder, i512* nocapture %quotient) local_unnamed_addr #2 {
entry:
  %0 = load i512, i512* %pdividend, align 8, !tbaa !21
  %dividend.sroa.0.0.extract.trunc = trunc i512 %0 to i64
  %dividend.sroa.9.0.extract.shift = lshr i512 %0, 64
  %dividend.sroa.9.0.extract.trunc = trunc i512 %dividend.sroa.9.0.extract.shift to i64
  %dividend.sroa.11.0.extract.shift = lshr i512 %0, 128
  %dividend.sroa.11.0.extract.trunc = trunc i512 %dividend.sroa.11.0.extract.shift to i64
  %dividend.sroa.13.0.extract.shift = lshr i512 %0, 192
  %dividend.sroa.13.0.extract.trunc = trunc i512 %dividend.sroa.13.0.extract.shift to i64
  %dividend.sroa.15.0.extract.shift = lshr i512 %0, 256
  %dividend.sroa.15.0.extract.trunc = trunc i512 %dividend.sroa.15.0.extract.shift to i64
  %dividend.sroa.17.0.extract.shift = lshr i512 %0, 320
  %dividend.sroa.17.0.extract.trunc = trunc i512 %dividend.sroa.17.0.extract.shift to i64
  %dividend.sroa.19.0.extract.shift = lshr i512 %0, 384
  %dividend.sroa.19.0.extract.trunc = trunc i512 %dividend.sroa.19.0.extract.shift to i64
  %dividend.sroa.21.0.extract.shift = lshr i512 %0, 448
  %dividend.sroa.21.0.extract.trunc = trunc i512 %dividend.sroa.21.0.extract.shift to i64
  %1 = load i512, i512* %pdivisor, align 8, !tbaa !21
  %divisor.sroa.0.0.extract.trunc = trunc i512 %1 to i64
  %divisor.sroa.6.0.extract.shift = lshr i512 %1, 64
  %divisor.sroa.6.0.extract.trunc = trunc i512 %divisor.sroa.6.0.extract.shift to i64
  %divisor.sroa.8.0.extract.shift = lshr i512 %1, 128
  %divisor.sroa.8.0.extract.trunc = trunc i512 %divisor.sroa.8.0.extract.shift to i64
  %divisor.sroa.10.0.extract.shift = lshr i512 %1, 192
  %divisor.sroa.10.0.extract.trunc = trunc i512 %divisor.sroa.10.0.extract.shift to i64
  %divisor.sroa.12.0.extract.shift = lshr i512 %1, 256
  %divisor.sroa.12.0.extract.trunc = trunc i512 %divisor.sroa.12.0.extract.shift to i64
  %divisor.sroa.14.0.extract.shift = lshr i512 %1, 320
  %divisor.sroa.14.0.extract.trunc = trunc i512 %divisor.sroa.14.0.extract.shift to i64
  %divisor.sroa.16.0.extract.shift = lshr i512 %1, 384
  %divisor.sroa.16.0.extract.trunc = trunc i512 %divisor.sroa.16.0.extract.shift to i64
  %divisor.sroa.18.0.extract.shift = lshr i512 %1, 448
  %divisor.sroa.18.0.extract.trunc = trunc i512 %divisor.sroa.18.0.extract.shift to i64
  switch i512 %1, label %if.end3 [
    i512 0, label %cleanup
    i512 1, label %if.then2
  ]

if.then2:                                         ; preds = %entry
  store i512 0, i512* %remainder, align 8, !tbaa !21
  store i512 %0, i512* %quotient, align 8, !tbaa !21
  br label %cleanup

if.end3:                                          ; preds = %entry
  %cmp4 = icmp eq i512 %1, %0
  br i1 %cmp4, label %if.then5, label %if.end6

if.then5:                                         ; preds = %if.end3
  store i512 0, i512* %remainder, align 8, !tbaa !21
  store i512 1, i512* %quotient, align 8, !tbaa !21
  br label %cleanup

if.end6:                                          ; preds = %if.end3
  %cmp7 = icmp eq i512 %0, 0
  %cmp8 = icmp ult i512 %0, %1
  %or.cond = or i1 %cmp7, %cmp8
  br i1 %or.cond, label %if.then9, label %if.end10

if.then9:                                         ; preds = %if.end6
  store i512 %0, i512* %remainder, align 8, !tbaa !21
  store i512 0, i512* %quotient, align 8, !tbaa !21
  br label %cleanup

if.end10:                                         ; preds = %if.end6
  %tobool.not.i = icmp eq i64 %dividend.sroa.21.0.extract.trunc, 0
  br i1 %tobool.not.i, label %for.inc.i, label %cleanup.i

for.inc.i:                                        ; preds = %if.end10
  %tobool.not.1.i = icmp eq i64 %dividend.sroa.19.0.extract.trunc, 0
  br i1 %tobool.not.1.i, label %for.inc.1.i, label %cleanup.i

cleanup.i:                                        ; preds = %for.inc.6.i, %for.inc.5.i, %for.inc.4.i, %for.inc.3.i, %for.inc.2.i, %for.inc.1.i, %for.inc.i, %if.end10
  %i.013.lcssa.i = phi i32 [ 448, %if.end10 ], [ 384, %for.inc.i ], [ 320, %for.inc.1.i ], [ 256, %for.inc.2.i ], [ 192, %for.inc.3.i ], [ 128, %for.inc.4.i ], [ 64, %for.inc.5.i ], [ 0, %for.inc.6.i ]
  %.lcssa.i = phi i64 [ %dividend.sroa.21.0.extract.trunc, %if.end10 ], [ %dividend.sroa.19.0.extract.trunc, %for.inc.i ], [ %dividend.sroa.17.0.extract.trunc, %for.inc.1.i ], [ %dividend.sroa.15.0.extract.trunc, %for.inc.2.i ], [ %dividend.sroa.13.0.extract.trunc, %for.inc.3.i ], [ %dividend.sroa.11.0.extract.trunc, %for.inc.4.i ], [ %dividend.sroa.9.0.extract.trunc, %for.inc.5.i ], [ %dividend.sroa.0.0.extract.trunc, %for.inc.6.i ]
  %tobool.not.i.i = icmp ult i64 %.lcssa.i, 4294967296
  %shl.i.i = shl i64 %.lcssa.i, 32
  %spec.select.i.i = select i1 %tobool.not.i.i, i64 %shl.i.i, i64 %.lcssa.i
  %spec.select47.i.i = select i1 %tobool.not.i.i, i32 31, i32 63
  %tobool2.not.i.i = icmp ult i64 %spec.select.i.i, 281474976710656
  %sub4.i.i = add nsw i32 %spec.select47.i.i, -16
  %shl5.i.i = shl i64 %spec.select.i.i, 16
  %v.addr.1.i.i = select i1 %tobool2.not.i.i, i64 %shl5.i.i, i64 %spec.select.i.i
  %h.1.i.i = select i1 %tobool2.not.i.i, i32 %sub4.i.i, i32 %spec.select47.i.i
  %tobool8.not.i.i = icmp ult i64 %v.addr.1.i.i, 72057594037927936
  %sub10.i.i = add nsw i32 %h.1.i.i, -8
  %shl11.i.i = shl i64 %v.addr.1.i.i, 8
  %v.addr.2.i.i = select i1 %tobool8.not.i.i, i64 %shl11.i.i, i64 %v.addr.1.i.i
  %h.2.i.i = select i1 %tobool8.not.i.i, i32 %sub10.i.i, i32 %h.1.i.i
  %tobool14.not.i.i = icmp ult i64 %v.addr.2.i.i, 1152921504606846976
  %sub16.i.i = add nsw i32 %h.2.i.i, -4
  %shl17.i.i = shl i64 %v.addr.2.i.i, 4
  %v.addr.3.i.i = select i1 %tobool14.not.i.i, i64 %shl17.i.i, i64 %v.addr.2.i.i
  %h.3.i.i = select i1 %tobool14.not.i.i, i32 %sub16.i.i, i32 %h.2.i.i
  %tobool20.not.i.i = icmp ult i64 %v.addr.3.i.i, 4611686018427387904
  %sub22.i.i = add nsw i32 %h.3.i.i, -2
  %shl23.i.i = shl i64 %v.addr.3.i.i, 2
  %v.addr.4.i.i = select i1 %tobool20.not.i.i, i64 %shl23.i.i, i64 %v.addr.3.i.i
  %h.4.i.i = select i1 %tobool20.not.i.i, i32 %sub22.i.i, i32 %h.3.i.i
  %v.addr.4.lobit.i.i = ashr i64 %v.addr.4.i.i, 63
  %2 = trunc i64 %v.addr.4.lobit.i.i to i32
  %.not.i.i = xor i32 %2, -1
  %spec.select48.i.i = add nuw nsw i32 %h.4.i.i, %i.013.lcssa.i
  %add.i = add i32 %spec.select48.i.i, %.not.i.i
  br label %bits512.exit

for.inc.1.i:                                      ; preds = %for.inc.i
  %tobool.not.2.i = icmp eq i64 %dividend.sroa.17.0.extract.trunc, 0
  br i1 %tobool.not.2.i, label %for.inc.2.i, label %cleanup.i

for.inc.2.i:                                      ; preds = %for.inc.1.i
  %tobool.not.3.i = icmp eq i64 %dividend.sroa.15.0.extract.trunc, 0
  br i1 %tobool.not.3.i, label %for.inc.3.i, label %cleanup.i

for.inc.3.i:                                      ; preds = %for.inc.2.i
  %tobool.not.4.i = icmp eq i64 %dividend.sroa.13.0.extract.trunc, 0
  br i1 %tobool.not.4.i, label %for.inc.4.i, label %cleanup.i

for.inc.4.i:                                      ; preds = %for.inc.3.i
  %tobool.not.5.i = icmp eq i64 %dividend.sroa.11.0.extract.trunc, 0
  br i1 %tobool.not.5.i, label %for.inc.5.i, label %cleanup.i

for.inc.5.i:                                      ; preds = %for.inc.4.i
  %tobool.not.6.i = icmp eq i64 %dividend.sroa.9.0.extract.trunc, 0
  br i1 %tobool.not.6.i, label %for.inc.6.i, label %cleanup.i

for.inc.6.i:                                      ; preds = %for.inc.5.i
  %tobool.not.7.i = icmp eq i64 %dividend.sroa.0.0.extract.trunc, 0
  br i1 %tobool.not.7.i, label %bits512.exit, label %cleanup.i

bits512.exit:                                     ; preds = %cleanup.i, %for.inc.6.i
  %3 = phi i32 [ %add.i, %cleanup.i ], [ 0, %for.inc.6.i ]
  %tobool.not.i159 = icmp eq i64 %divisor.sroa.18.0.extract.trunc, 0
  br i1 %tobool.not.i159, label %for.inc.i162, label %cleanup.i193

for.inc.i162:                                     ; preds = %bits512.exit
  %tobool.not.1.i161 = icmp eq i64 %divisor.sroa.16.0.extract.trunc, 0
  br i1 %tobool.not.1.i161, label %for.inc.1.i196, label %cleanup.i193

cleanup.i193:                                     ; preds = %for.inc.6.i210, %for.inc.5.i208, %for.inc.4.i205, %for.inc.3.i202, %for.inc.2.i199, %for.inc.1.i196, %for.inc.i162, %bits512.exit
  %i.013.lcssa.i163 = phi i32 [ 448, %bits512.exit ], [ 384, %for.inc.i162 ], [ 320, %for.inc.1.i196 ], [ 256, %for.inc.2.i199 ], [ 192, %for.inc.3.i202 ], [ 128, %for.inc.4.i205 ], [ 64, %for.inc.5.i208 ], [ 0, %for.inc.6.i210 ]
  %.lcssa.i164 = phi i64 [ %divisor.sroa.18.0.extract.trunc, %bits512.exit ], [ %divisor.sroa.16.0.extract.trunc, %for.inc.i162 ], [ %divisor.sroa.14.0.extract.trunc, %for.inc.1.i196 ], [ %divisor.sroa.12.0.extract.trunc, %for.inc.2.i199 ], [ %divisor.sroa.10.0.extract.trunc, %for.inc.3.i202 ], [ %divisor.sroa.8.0.extract.trunc, %for.inc.4.i205 ], [ %divisor.sroa.6.0.extract.trunc, %for.inc.5.i208 ], [ %divisor.sroa.0.0.extract.trunc, %for.inc.6.i210 ]
  %tobool.not.i.i165 = icmp ult i64 %.lcssa.i164, 4294967296
  %shl.i.i166 = shl i64 %.lcssa.i164, 32
  %spec.select.i.i167 = select i1 %tobool.not.i.i165, i64 %shl.i.i166, i64 %.lcssa.i164
  %spec.select47.i.i168 = select i1 %tobool.not.i.i165, i32 31, i32 63
  %tobool2.not.i.i169 = icmp ult i64 %spec.select.i.i167, 281474976710656
  %sub4.i.i170 = add nsw i32 %spec.select47.i.i168, -16
  %shl5.i.i171 = shl i64 %spec.select.i.i167, 16
  %v.addr.1.i.i172 = select i1 %tobool2.not.i.i169, i64 %shl5.i.i171, i64 %spec.select.i.i167
  %h.1.i.i173 = select i1 %tobool2.not.i.i169, i32 %sub4.i.i170, i32 %spec.select47.i.i168
  %tobool8.not.i.i174 = icmp ult i64 %v.addr.1.i.i172, 72057594037927936
  %sub10.i.i175 = add nsw i32 %h.1.i.i173, -8
  %shl11.i.i176 = shl i64 %v.addr.1.i.i172, 8
  %v.addr.2.i.i177 = select i1 %tobool8.not.i.i174, i64 %shl11.i.i176, i64 %v.addr.1.i.i172
  %h.2.i.i178 = select i1 %tobool8.not.i.i174, i32 %sub10.i.i175, i32 %h.1.i.i173
  %tobool14.not.i.i179 = icmp ult i64 %v.addr.2.i.i177, 1152921504606846976
  %sub16.i.i180 = add nsw i32 %h.2.i.i178, -4
  %shl17.i.i181 = shl i64 %v.addr.2.i.i177, 4
  %v.addr.3.i.i182 = select i1 %tobool14.not.i.i179, i64 %shl17.i.i181, i64 %v.addr.2.i.i177
  %h.3.i.i183 = select i1 %tobool14.not.i.i179, i32 %sub16.i.i180, i32 %h.2.i.i178
  %tobool20.not.i.i184 = icmp ult i64 %v.addr.3.i.i182, 4611686018427387904
  %sub22.i.i185 = add nsw i32 %h.3.i.i183, -2
  %shl23.i.i186 = shl i64 %v.addr.3.i.i182, 2
  %v.addr.4.i.i187 = select i1 %tobool20.not.i.i184, i64 %shl23.i.i186, i64 %v.addr.3.i.i182
  %h.4.i.i188 = select i1 %tobool20.not.i.i184, i32 %sub22.i.i185, i32 %h.3.i.i183
  %v.addr.4.lobit.i.i189 = ashr i64 %v.addr.4.i.i187, 63
  %4 = trunc i64 %v.addr.4.lobit.i.i189 to i32
  %.not.i.i190 = xor i32 %4, -1
  %spec.select48.i.i191 = add nuw nsw i32 %h.4.i.i188, %i.013.lcssa.i163
  %add.i192 = add i32 %spec.select48.i.i191, %.not.i.i190
  br label %bits512.exit211

for.inc.1.i196:                                   ; preds = %for.inc.i162
  %tobool.not.2.i195 = icmp eq i64 %divisor.sroa.14.0.extract.trunc, 0
  br i1 %tobool.not.2.i195, label %for.inc.2.i199, label %cleanup.i193

for.inc.2.i199:                                   ; preds = %for.inc.1.i196
  %tobool.not.3.i198 = icmp eq i64 %divisor.sroa.12.0.extract.trunc, 0
  br i1 %tobool.not.3.i198, label %for.inc.3.i202, label %cleanup.i193

for.inc.3.i202:                                   ; preds = %for.inc.2.i199
  %tobool.not.4.i201 = icmp eq i64 %divisor.sroa.10.0.extract.trunc, 0
  br i1 %tobool.not.4.i201, label %for.inc.4.i205, label %cleanup.i193

for.inc.4.i205:                                   ; preds = %for.inc.3.i202
  %tobool.not.5.i204 = icmp eq i64 %divisor.sroa.8.0.extract.trunc, 0
  br i1 %tobool.not.5.i204, label %for.inc.5.i208, label %cleanup.i193

for.inc.5.i208:                                   ; preds = %for.inc.4.i205
  %tobool.not.6.i207 = icmp eq i64 %divisor.sroa.6.0.extract.trunc, 0
  br i1 %tobool.not.6.i207, label %for.inc.6.i210, label %cleanup.i193

for.inc.6.i210:                                   ; preds = %for.inc.5.i208
  %tobool.not.7.i209 = icmp eq i64 %divisor.sroa.0.0.extract.trunc, 0
  br i1 %tobool.not.7.i209, label %bits512.exit211, label %cleanup.i193

bits512.exit211:                                  ; preds = %cleanup.i193, %for.inc.6.i210
  %5 = phi i32 [ %add.i192, %cleanup.i193 ], [ 0, %for.inc.6.i210 ]
  %sub = sub nsw i32 %3, %5
  %sh_prom = zext i32 %sub to i512
  %shl = shl i512 %1, %sh_prom
  br i1 %tobool.not.i, label %for.inc.i108, label %cleanup.i139

for.inc.i108:                                     ; preds = %bits512.exit211
  %tobool.not.1.i107 = icmp eq i64 %dividend.sroa.19.0.extract.trunc, 0
  br i1 %tobool.not.1.i107, label %for.inc.1.i142, label %cleanup.i139

cleanup.i139:                                     ; preds = %for.inc.6.i156, %for.inc.5.i154, %for.inc.4.i151, %for.inc.3.i148, %for.inc.2.i145, %for.inc.1.i142, %for.inc.i108, %bits512.exit211
  %i.013.lcssa.i109 = phi i32 [ 448, %bits512.exit211 ], [ 384, %for.inc.i108 ], [ 320, %for.inc.1.i142 ], [ 256, %for.inc.2.i145 ], [ 192, %for.inc.3.i148 ], [ 128, %for.inc.4.i151 ], [ 64, %for.inc.5.i154 ], [ 0, %for.inc.6.i156 ]
  %.lcssa.i110 = phi i64 [ %dividend.sroa.21.0.extract.trunc, %bits512.exit211 ], [ %dividend.sroa.19.0.extract.trunc, %for.inc.i108 ], [ %dividend.sroa.17.0.extract.trunc, %for.inc.1.i142 ], [ %dividend.sroa.15.0.extract.trunc, %for.inc.2.i145 ], [ %dividend.sroa.13.0.extract.trunc, %for.inc.3.i148 ], [ %dividend.sroa.11.0.extract.trunc, %for.inc.4.i151 ], [ %dividend.sroa.9.0.extract.trunc, %for.inc.5.i154 ], [ %dividend.sroa.0.0.extract.trunc, %for.inc.6.i156 ]
  %tobool.not.i.i111 = icmp ult i64 %.lcssa.i110, 4294967296
  %shl.i.i112 = shl i64 %.lcssa.i110, 32
  %spec.select.i.i113 = select i1 %tobool.not.i.i111, i64 %shl.i.i112, i64 %.lcssa.i110
  %spec.select47.i.i114 = select i1 %tobool.not.i.i111, i32 31, i32 63
  %tobool2.not.i.i115 = icmp ult i64 %spec.select.i.i113, 281474976710656
  %sub4.i.i116 = add nsw i32 %spec.select47.i.i114, -16
  %shl5.i.i117 = shl i64 %spec.select.i.i113, 16
  %v.addr.1.i.i118 = select i1 %tobool2.not.i.i115, i64 %shl5.i.i117, i64 %spec.select.i.i113
  %h.1.i.i119 = select i1 %tobool2.not.i.i115, i32 %sub4.i.i116, i32 %spec.select47.i.i114
  %tobool8.not.i.i120 = icmp ult i64 %v.addr.1.i.i118, 72057594037927936
  %sub10.i.i121 = add nsw i32 %h.1.i.i119, -8
  %shl11.i.i122 = shl i64 %v.addr.1.i.i118, 8
  %v.addr.2.i.i123 = select i1 %tobool8.not.i.i120, i64 %shl11.i.i122, i64 %v.addr.1.i.i118
  %h.2.i.i124 = select i1 %tobool8.not.i.i120, i32 %sub10.i.i121, i32 %h.1.i.i119
  %tobool14.not.i.i125 = icmp ult i64 %v.addr.2.i.i123, 1152921504606846976
  %sub16.i.i126 = add nsw i32 %h.2.i.i124, -4
  %shl17.i.i127 = shl i64 %v.addr.2.i.i123, 4
  %v.addr.3.i.i128 = select i1 %tobool14.not.i.i125, i64 %shl17.i.i127, i64 %v.addr.2.i.i123
  %h.3.i.i129 = select i1 %tobool14.not.i.i125, i32 %sub16.i.i126, i32 %h.2.i.i124
  %tobool20.not.i.i130 = icmp ult i64 %v.addr.3.i.i128, 4611686018427387904
  %sub22.i.i131 = add nsw i32 %h.3.i.i129, -2
  %shl23.i.i132 = shl i64 %v.addr.3.i.i128, 2
  %v.addr.4.i.i133 = select i1 %tobool20.not.i.i130, i64 %shl23.i.i132, i64 %v.addr.3.i.i128
  %h.4.i.i134 = select i1 %tobool20.not.i.i130, i32 %sub22.i.i131, i32 %h.3.i.i129
  %v.addr.4.lobit.i.i135 = ashr i64 %v.addr.4.i.i133, 63
  %6 = trunc i64 %v.addr.4.lobit.i.i135 to i32
  %.not.i.i136 = xor i32 %6, -1
  %spec.select48.i.i137 = add nuw nsw i32 %h.4.i.i134, %i.013.lcssa.i109
  %add.i138 = add i32 %spec.select48.i.i137, %.not.i.i136
  br label %bits512.exit157

for.inc.1.i142:                                   ; preds = %for.inc.i108
  %tobool.not.2.i141 = icmp eq i64 %dividend.sroa.17.0.extract.trunc, 0
  br i1 %tobool.not.2.i141, label %for.inc.2.i145, label %cleanup.i139

for.inc.2.i145:                                   ; preds = %for.inc.1.i142
  %tobool.not.3.i144 = icmp eq i64 %dividend.sroa.15.0.extract.trunc, 0
  br i1 %tobool.not.3.i144, label %for.inc.3.i148, label %cleanup.i139

for.inc.3.i148:                                   ; preds = %for.inc.2.i145
  %tobool.not.4.i147 = icmp eq i64 %dividend.sroa.13.0.extract.trunc, 0
  br i1 %tobool.not.4.i147, label %for.inc.4.i151, label %cleanup.i139

for.inc.4.i151:                                   ; preds = %for.inc.3.i148
  %tobool.not.5.i150 = icmp eq i64 %dividend.sroa.11.0.extract.trunc, 0
  br i1 %tobool.not.5.i150, label %for.inc.5.i154, label %cleanup.i139

for.inc.5.i154:                                   ; preds = %for.inc.4.i151
  %tobool.not.6.i153 = icmp eq i64 %dividend.sroa.9.0.extract.trunc, 0
  br i1 %tobool.not.6.i153, label %for.inc.6.i156, label %cleanup.i139

for.inc.6.i156:                                   ; preds = %for.inc.5.i154
  %tobool.not.7.i155 = icmp eq i64 %dividend.sroa.0.0.extract.trunc, 0
  br i1 %tobool.not.7.i155, label %bits512.exit157, label %cleanup.i139

bits512.exit157:                                  ; preds = %cleanup.i139, %for.inc.6.i156
  %7 = phi i32 [ %add.i138, %cleanup.i139 ], [ 0, %for.inc.6.i156 ]
  br i1 %tobool.not.i159, label %for.inc.i54, label %cleanup.i85

for.inc.i54:                                      ; preds = %bits512.exit157
  %tobool.not.1.i53 = icmp eq i64 %divisor.sroa.16.0.extract.trunc, 0
  br i1 %tobool.not.1.i53, label %for.inc.1.i88, label %cleanup.i85

cleanup.i85:                                      ; preds = %for.inc.6.i102, %for.inc.5.i100, %for.inc.4.i97, %for.inc.3.i94, %for.inc.2.i91, %for.inc.1.i88, %for.inc.i54, %bits512.exit157
  %i.013.lcssa.i55 = phi i32 [ 448, %bits512.exit157 ], [ 384, %for.inc.i54 ], [ 320, %for.inc.1.i88 ], [ 256, %for.inc.2.i91 ], [ 192, %for.inc.3.i94 ], [ 128, %for.inc.4.i97 ], [ 64, %for.inc.5.i100 ], [ 0, %for.inc.6.i102 ]
  %.lcssa.i56 = phi i64 [ %divisor.sroa.18.0.extract.trunc, %bits512.exit157 ], [ %divisor.sroa.16.0.extract.trunc, %for.inc.i54 ], [ %divisor.sroa.14.0.extract.trunc, %for.inc.1.i88 ], [ %divisor.sroa.12.0.extract.trunc, %for.inc.2.i91 ], [ %divisor.sroa.10.0.extract.trunc, %for.inc.3.i94 ], [ %divisor.sroa.8.0.extract.trunc, %for.inc.4.i97 ], [ %divisor.sroa.6.0.extract.trunc, %for.inc.5.i100 ], [ %divisor.sroa.0.0.extract.trunc, %for.inc.6.i102 ]
  %tobool.not.i.i57 = icmp ult i64 %.lcssa.i56, 4294967296
  %shl.i.i58 = shl i64 %.lcssa.i56, 32
  %spec.select.i.i59 = select i1 %tobool.not.i.i57, i64 %shl.i.i58, i64 %.lcssa.i56
  %spec.select47.i.i60 = select i1 %tobool.not.i.i57, i32 31, i32 63
  %tobool2.not.i.i61 = icmp ult i64 %spec.select.i.i59, 281474976710656
  %sub4.i.i62 = add nsw i32 %spec.select47.i.i60, -16
  %shl5.i.i63 = shl i64 %spec.select.i.i59, 16
  %v.addr.1.i.i64 = select i1 %tobool2.not.i.i61, i64 %shl5.i.i63, i64 %spec.select.i.i59
  %h.1.i.i65 = select i1 %tobool2.not.i.i61, i32 %sub4.i.i62, i32 %spec.select47.i.i60
  %tobool8.not.i.i66 = icmp ult i64 %v.addr.1.i.i64, 72057594037927936
  %sub10.i.i67 = add nsw i32 %h.1.i.i65, -8
  %shl11.i.i68 = shl i64 %v.addr.1.i.i64, 8
  %v.addr.2.i.i69 = select i1 %tobool8.not.i.i66, i64 %shl11.i.i68, i64 %v.addr.1.i.i64
  %h.2.i.i70 = select i1 %tobool8.not.i.i66, i32 %sub10.i.i67, i32 %h.1.i.i65
  %tobool14.not.i.i71 = icmp ult i64 %v.addr.2.i.i69, 1152921504606846976
  %sub16.i.i72 = add nsw i32 %h.2.i.i70, -4
  %shl17.i.i73 = shl i64 %v.addr.2.i.i69, 4
  %v.addr.3.i.i74 = select i1 %tobool14.not.i.i71, i64 %shl17.i.i73, i64 %v.addr.2.i.i69
  %h.3.i.i75 = select i1 %tobool14.not.i.i71, i32 %sub16.i.i72, i32 %h.2.i.i70
  %tobool20.not.i.i76 = icmp ult i64 %v.addr.3.i.i74, 4611686018427387904
  %sub22.i.i77 = add nsw i32 %h.3.i.i75, -2
  %shl23.i.i78 = shl i64 %v.addr.3.i.i74, 2
  %v.addr.4.i.i79 = select i1 %tobool20.not.i.i76, i64 %shl23.i.i78, i64 %v.addr.3.i.i74
  %h.4.i.i80 = select i1 %tobool20.not.i.i76, i32 %sub22.i.i77, i32 %h.3.i.i75
  %v.addr.4.lobit.i.i81 = ashr i64 %v.addr.4.i.i79, 63
  %8 = trunc i64 %v.addr.4.lobit.i.i81 to i32
  %.not.i.i82 = xor i32 %8, -1
  %spec.select48.i.i83 = add nuw nsw i32 %h.4.i.i80, %i.013.lcssa.i55
  %add.i84 = add i32 %spec.select48.i.i83, %.not.i.i82
  br label %while.body.preheader

for.inc.1.i88:                                    ; preds = %for.inc.i54
  %tobool.not.2.i87 = icmp eq i64 %divisor.sroa.14.0.extract.trunc, 0
  br i1 %tobool.not.2.i87, label %for.inc.2.i91, label %cleanup.i85

for.inc.2.i91:                                    ; preds = %for.inc.1.i88
  %tobool.not.3.i90 = icmp eq i64 %divisor.sroa.12.0.extract.trunc, 0
  br i1 %tobool.not.3.i90, label %for.inc.3.i94, label %cleanup.i85

for.inc.3.i94:                                    ; preds = %for.inc.2.i91
  %tobool.not.4.i93 = icmp eq i64 %divisor.sroa.10.0.extract.trunc, 0
  br i1 %tobool.not.4.i93, label %for.inc.4.i97, label %cleanup.i85

for.inc.4.i97:                                    ; preds = %for.inc.3.i94
  %tobool.not.5.i96 = icmp eq i64 %divisor.sroa.8.0.extract.trunc, 0
  br i1 %tobool.not.5.i96, label %for.inc.5.i100, label %cleanup.i85

for.inc.5.i100:                                   ; preds = %for.inc.4.i97
  %tobool.not.6.i99 = icmp eq i64 %divisor.sroa.6.0.extract.trunc, 0
  br i1 %tobool.not.6.i99, label %for.inc.6.i102, label %cleanup.i85

for.inc.6.i102:                                   ; preds = %for.inc.5.i100
  %tobool.not.7.i101 = icmp eq i64 %divisor.sroa.0.0.extract.trunc, 0
  br i1 %tobool.not.7.i101, label %while.body.preheader, label %cleanup.i85

while.body.preheader:                             ; preds = %for.inc.6.i102, %cleanup.i85
  %9 = phi i32 [ %add.i84, %cleanup.i85 ], [ 0, %for.inc.6.i102 ]
  %cmp17 = icmp ugt i512 %shl, %0
  %shr = zext i1 %cmp17 to i512
  %sub14 = sub nsw i32 %7, %9
  %sh_prom15 = zext i32 %sub14 to i512
  %shl16 = shl nuw i512 1, %sh_prom15
  %adder.0 = lshr i512 %shl16, %shr
  %copyd.0 = lshr i512 %shl, %shr
  br label %while.body

while.body:                                       ; preds = %while.body.preheader, %while.body
  %adder.1367 = phi i512 [ %shr27, %while.body ], [ %adder.0, %while.body.preheader ]
  %copyd.1366 = phi i512 [ %shr26, %while.body ], [ %copyd.0, %while.body.preheader ]
  %r.0365 = phi i512 [ %r.1, %while.body ], [ %0, %while.body.preheader ]
  %q.0364 = phi i512 [ %q.1, %while.body ], [ 0, %while.body.preheader ]
  %cmp22.not = icmp ult i512 %r.0365, %copyd.1366
  %or = select i1 %cmp22.not, i512 0, i512 %adder.1367
  %q.1 = or i512 %or, %q.0364
  %sub24 = select i1 %cmp22.not, i512 0, i512 %copyd.1366
  %r.1 = sub i512 %r.0365, %sub24
  %shr26 = lshr i512 %copyd.1366, 1
  %shr27 = lshr i512 %adder.1367, 1
  %cmp21.not = icmp ult i512 %r.1, %1
  br i1 %cmp21.not, label %while.end, label %while.body

while.end:                                        ; preds = %while.body
  store i512 %q.1, i512* %quotient, align 8, !tbaa !21
  store i512 %r.1, i512* %remainder, align 8, !tbaa !21
  br label %cleanup

cleanup:                                          ; preds = %entry, %while.end, %if.then9, %if.then5, %if.then2
  %retval.0 = phi i32 [ 0, %if.then2 ], [ 0, %if.then5 ], [ 0, %if.then9 ], [ 0, %while.end ], [ 1, %entry ]
  ret i32 %retval.0
}

; Function Attrs: nounwind
define internal i32 @sdivmod512(i512* nocapture %pdividend, i512* nocapture %pdivisor, i512* nocapture %remainder, i512* nocapture %quotient) local_unnamed_addr #2 {
entry:
  %0 = bitcast i512* %pdividend to i8*
  %arrayidx = getelementptr inbounds i8, i8* %0, i32 63
  %1 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %cmp = icmp slt i8 %1, 0
  br i1 %cmp, label %if.then, label %if.end

if.then:                                          ; preds = %entry
  %2 = load i512, i512* %pdividend, align 8, !tbaa !21
  %sub = sub i512 0, %2
  store i512 %sub, i512* %pdividend, align 8, !tbaa !21
  br label %if.end

if.end:                                           ; preds = %if.then, %entry
  %3 = bitcast i512* %pdivisor to i8*
  %arrayidx2 = getelementptr inbounds i8, i8* %3, i32 63
  %4 = load i8, i8* %arrayidx2, align 1, !tbaa !7
  %cmp4 = icmp slt i8 %4, 0
  br i1 %cmp4, label %if.then8, label %if.end10

if.then8:                                         ; preds = %if.end
  %5 = load i512, i512* %pdivisor, align 8, !tbaa !21
  %sub9 = sub i512 0, %5
  store i512 %sub9, i512* %pdivisor, align 8, !tbaa !21
  br label %if.end10

if.end10:                                         ; preds = %if.then8, %if.end
  %call = tail call i32 @udivmod512(i512* nonnull %pdividend, i512* nonnull %pdivisor, i512* %remainder, i512* %quotient) #15
  %tobool11.not = icmp eq i32 %call, 0
  br i1 %tobool11.not, label %if.end13, label %cleanup

if.end13:                                         ; preds = %if.end10
  %cmp18.not.unshifted = xor i8 %4, %1
  %cmp18.not = icmp sgt i8 %cmp18.not.unshifted, -1
  br i1 %cmp18.not, label %if.end22, label %if.then20

if.then20:                                        ; preds = %if.end13
  %6 = load i512, i512* %quotient, align 8, !tbaa !21
  %sub21 = sub i512 0, %6
  store i512 %sub21, i512* %quotient, align 8, !tbaa !21
  br label %if.end22

if.end22:                                         ; preds = %if.end13, %if.then20
  br i1 %cmp, label %if.then24, label %cleanup

if.then24:                                        ; preds = %if.end22
  %7 = load i512, i512* %remainder, align 8, !tbaa !21
  %sub25 = sub i512 0, %7
  store i512 %sub25, i512* %remainder, align 8, !tbaa !21
  br label %cleanup

cleanup:                                          ; preds = %if.end22, %if.then24, %if.end10
  %retval.0 = phi i32 [ 1, %if.end10 ], [ 0, %if.then24 ], [ 0, %if.end22 ]
  ret i32 %retval.0
}

; Function Attrs: nofree norecurse nounwind
define internal void @hex_encode(i8* nocapture %output, i8* nocapture readonly %input, i32 %length) local_unnamed_addr #1 {
entry:
  %cmp34.not = icmp eq i32 %length, 0
  br i1 %cmp34.not, label %for.cond.cleanup, label %for.body

for.cond.cleanup:                                 ; preds = %for.body, %entry
  ret void

for.body:                                         ; preds = %entry, %for.body
  %output.addr.036 = phi i8* [ %incdec.ptr25, %for.body ], [ %output, %entry ]
  %i.035 = phi i32 [ %inc, %for.body ], [ 0, %entry ]
  %arrayidx = getelementptr inbounds i8, i8* %input, i32 %i.035
  %0 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %1 = lshr i8 %0, 4
  %cmp3 = icmp ugt i8 %0, -97
  %sub = add nuw nsw i8 %1, 87
  %add7 = or i8 %1, 48
  %cond = select i1 %cmp3, i8 %sub, i8 %add7
  %incdec.ptr = getelementptr inbounds i8, i8* %output.addr.036, i32 1
  store i8 %cond, i8* %output.addr.036, align 1, !tbaa !7
  %2 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %3 = and i8 %2, 15
  %cmp13 = icmp ugt i8 %3, 9
  %sub18 = add nuw nsw i8 %3, 87
  %add21 = or i8 %3, 48
  %cond23 = select i1 %cmp13, i8 %sub18, i8 %add21
  %incdec.ptr25 = getelementptr inbounds i8, i8* %output.addr.036, i32 2
  store i8 %cond23, i8* %incdec.ptr, align 1, !tbaa !7
  %inc = add nuw nsw i32 %i.035, 1
  %exitcond.not = icmp eq i32 %inc, %length
  br i1 %exitcond.not, label %for.cond.cleanup, label %for.body
}

; Function Attrs: nofree norecurse nounwind
define internal void @hex_encode_rev(i8* nocapture %output, i8* nocapture readonly %input, i32 %length) local_unnamed_addr #1 {
entry:
  %i.035 = add i32 %length, -1
  %cmp36 = icmp sgt i32 %i.035, -1
  br i1 %cmp36, label %for.body, label %for.cond.cleanup

for.cond.cleanup:                                 ; preds = %for.body, %entry
  ret void

for.body:                                         ; preds = %entry, %for.body
  %i.038 = phi i32 [ %i.0, %for.body ], [ %i.035, %entry ]
  %output.addr.037 = phi i8* [ %incdec.ptr26, %for.body ], [ %output, %entry ]
  %arrayidx = getelementptr inbounds i8, i8* %input, i32 %i.038
  %0 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %1 = lshr i8 %0, 4
  %cmp3 = icmp ugt i8 %0, -97
  %sub6 = add nuw nsw i8 %1, 87
  %add8 = or i8 %1, 48
  %cond = select i1 %cmp3, i8 %sub6, i8 %add8
  %incdec.ptr = getelementptr inbounds i8, i8* %output.addr.037, i32 1
  store i8 %cond, i8* %output.addr.037, align 1, !tbaa !7
  %2 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %3 = and i8 %2, 15
  %cmp14 = icmp ugt i8 %3, 9
  %sub19 = add nuw nsw i8 %3, 87
  %add22 = or i8 %3, 48
  %cond24 = select i1 %cmp14, i8 %sub19, i8 %add22
  %incdec.ptr26 = getelementptr inbounds i8, i8* %output.addr.037, i32 2
  store i8 %cond24, i8* %incdec.ptr, align 1, !tbaa !7
  %i.0 = add nsw i32 %i.038, -1
  %cmp = icmp sgt i32 %i.038, 0
  br i1 %cmp, label %for.body, label %for.cond.cleanup
}

; Function Attrs: nofree norecurse nounwind
define internal nonnull i8* @uint2hex(i8* %output, i8* nocapture readonly %input, i32 %length) local_unnamed_addr #1 {
entry:
  %0 = icmp ne i32 %length, 0
  %umin = zext i1 %0 to i32
  br label %while.cond

while.cond:                                       ; preds = %land.rhs, %entry
  %length.addr.0 = phi i32 [ %length, %entry ], [ %sub, %land.rhs ]
  %cmp = icmp ugt i32 %length.addr.0, 1
  br i1 %cmp, label %land.rhs, label %while.cond.while.end_crit_edge

while.cond.while.end_crit_edge:                   ; preds = %while.cond
  %not. = xor i1 %0, true
  %.pre112 = sext i1 %not. to i32
  br label %while.end

land.rhs:                                         ; preds = %while.cond
  %sub = add i32 %length.addr.0, -1
  %arrayidx = getelementptr inbounds i8, i8* %input, i32 %sub
  %1 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %cmp1 = icmp eq i8 %1, 0
  br i1 %cmp1, label %while.cond, label %while.end

while.end:                                        ; preds = %land.rhs, %while.cond.while.end_crit_edge
  %sub4.pre-phi = phi i32 [ %.pre112, %while.cond.while.end_crit_edge ], [ %sub, %land.rhs ]
  %length.addr.0.lcssa = phi i32 [ %umin, %while.cond.while.end_crit_edge ], [ %length.addr.0, %land.rhs ]
  %incdec.ptr = getelementptr inbounds i8, i8* %output, i32 1
  store i8 48, i8* %output, align 1, !tbaa !7
  %incdec.ptr3 = getelementptr inbounds i8, i8* %output, i32 2
  store i8 120, i8* %incdec.ptr, align 1, !tbaa !7
  %arrayidx5 = getelementptr inbounds i8, i8* %input, i32 %sub4.pre-phi
  %2 = load i8, i8* %arrayidx5, align 1, !tbaa !7
  %3 = lshr i8 %2, 4
  %cmp9.not = icmp eq i8 %3, 0
  br i1 %cmp9.not, label %if.end, label %if.then

if.then:                                          ; preds = %while.end
  %cmp12 = icmp ugt i8 %2, -97
  %sub15 = add nuw nsw i8 %3, 87
  %add17 = or i8 %3, 48
  %cond = select i1 %cmp12, i8 %sub15, i8 %add17
  %incdec.ptr19 = getelementptr inbounds i8, i8* %output, i32 3
  store i8 %cond, i8* %incdec.ptr3, align 1, !tbaa !7
  %.pre = load i8, i8* %arrayidx5, align 1, !tbaa !7
  br label %if.end

if.end:                                           ; preds = %while.end, %if.then
  %4 = phi i8 [ %.pre, %if.then ], [ %2, %while.end ]
  %output.addr.0 = phi i8* [ %incdec.ptr19, %if.then ], [ %incdec.ptr3, %while.end ]
  %5 = and i8 %4, 15
  %cmp25 = icmp ugt i8 %5, 9
  %sub30 = add nuw nsw i8 %5, 87
  %add33 = or i8 %5, 48
  %cond35 = select i1 %cmp25, i8 %sub30, i8 %add33
  store i8 %cond35, i8* %output.addr.0, align 1, !tbaa !7
  %output.addr.1104 = getelementptr inbounds i8, i8* %output.addr.0, i32 1
  %tobool.not106 = icmp eq i32 %sub4.pre-phi, 0
  br i1 %tobool.not106, label %while.end81, label %while.body40

while.body40:                                     ; preds = %if.end, %while.body40
  %dec39110 = phi i32 [ %dec39, %while.body40 ], [ %sub4.pre-phi, %if.end ]
  %output.addr.1109 = phi i8* [ %output.addr.1, %while.body40 ], [ %output.addr.1104, %if.end ]
  %output.addr.0.pn108 = phi i8* [ %incdec.ptr60, %while.body40 ], [ %output.addr.0, %if.end ]
  %length.addr.1107 = phi i32 [ %dec39110, %while.body40 ], [ %length.addr.0.lcssa, %if.end ]
  %sub42 = add i32 %length.addr.1107, -2
  %arrayidx43 = getelementptr inbounds i8, i8* %input, i32 %sub42
  %6 = load i8, i8* %arrayidx43, align 1, !tbaa !7
  %7 = lshr i8 %6, 4
  %cmp48 = icmp ugt i8 %6, -97
  %sub53 = add nuw nsw i8 %7, 87
  %add56 = or i8 %7, 48
  %cond58 = select i1 %cmp48, i8 %sub53, i8 %add56
  %incdec.ptr60 = getelementptr inbounds i8, i8* %output.addr.0.pn108, i32 2
  store i8 %cond58, i8* %output.addr.1109, align 1, !tbaa !7
  %8 = load i8, i8* %arrayidx43, align 1, !tbaa !7
  %9 = and i8 %8, 15
  %cmp68 = icmp ugt i8 %9, 9
  %sub73 = add nuw nsw i8 %9, 87
  %add76 = or i8 %9, 48
  %cond78 = select i1 %cmp68, i8 %sub73, i8 %add76
  store i8 %cond78, i8* %incdec.ptr60, align 1, !tbaa !7
  %output.addr.1 = getelementptr inbounds i8, i8* %output.addr.0.pn108, i32 3
  %dec39 = add i32 %dec39110, -1
  %tobool.not = icmp eq i32 %dec39, 0
  br i1 %tobool.not, label %while.end81, label %while.body40

while.end81:                                      ; preds = %while.body40, %if.end
  %output.addr.1.lcssa = phi i8* [ %output.addr.1104, %if.end ], [ %output.addr.1, %while.body40 ]
  ret i8* %output.addr.1.lcssa
}

; Function Attrs: nofree norecurse nounwind
define internal nonnull i8* @uint2bin(i8* %output, i8* nocapture readonly %input, i32 %length) local_unnamed_addr #1 {
entry:
  %0 = icmp ne i32 %length, 0
  %umin = zext i1 %0 to i32
  br label %while.cond

while.cond:                                       ; preds = %land.rhs, %entry
  %length.addr.0 = phi i32 [ %length, %entry ], [ %sub, %land.rhs ]
  %cmp = icmp ugt i32 %length.addr.0, 1
  br i1 %cmp, label %land.rhs, label %while.cond.while.end_crit_edge

while.cond.while.end_crit_edge:                   ; preds = %while.cond
  %not. = xor i1 %0, true
  %.pre = sext i1 %not. to i32
  br label %while.end

land.rhs:                                         ; preds = %while.cond
  %sub = add i32 %length.addr.0, -1
  %arrayidx = getelementptr inbounds i8, i8* %input, i32 %sub
  %1 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %cmp1 = icmp eq i8 %1, 0
  br i1 %cmp1, label %while.cond, label %while.end

while.end:                                        ; preds = %land.rhs, %while.cond.while.end_crit_edge
  %sub4.pre-phi = phi i32 [ %.pre, %while.cond.while.end_crit_edge ], [ %sub, %land.rhs ]
  %length.addr.0.lcssa = phi i32 [ %umin, %while.cond.while.end_crit_edge ], [ %length.addr.0, %land.rhs ]
  %incdec.ptr = getelementptr inbounds i8, i8* %output, i32 1
  store i8 48, i8* %output, align 1, !tbaa !7
  %incdec.ptr3 = getelementptr inbounds i8, i8* %output, i32 2
  store i8 98, i8* %incdec.ptr, align 1, !tbaa !7
  %arrayidx5 = getelementptr inbounds i8, i8* %input, i32 %sub4.pre-phi
  %2 = load i8, i8* %arrayidx5, align 1, !tbaa !7
  %tobool.not82 = icmp sgt i8 %2, -1
  br i1 %tobool.not82, label %while.body12, label %while.body20.preheader

while.cond17.preheader:                           ; preds = %while.body12
  %tobool19.not77 = icmp eq i32 %dec15, 0
  br i1 %tobool19.not77, label %while.cond30.preheader, label %while.body20.preheader

while.body20.preheader:                           ; preds = %while.end, %while.cond17.preheader
  %i.180.ph = phi i32 [ 8, %while.end ], [ %dec15, %while.cond17.preheader ]
  %v.179.ph = phi i8 [ %2, %while.end ], [ %shl, %while.cond17.preheader ]
  br label %while.body20

while.body12:                                     ; preds = %while.end, %while.body12
  %i.084 = phi i32 [ %dec15, %while.body12 ], [ 8, %while.end ]
  %v.083 = phi i8 [ %shl, %while.body12 ], [ %2, %while.end ]
  %shl = shl nuw i8 %v.083, 1
  %dec15 = add nsw i32 %i.084, -1
  %cmp7 = icmp ugt i32 %i.084, 1
  %tobool.not = icmp sgt i8 %shl, -1
  %3 = and i1 %tobool.not, %cmp7
  br i1 %3, label %while.body12, label %while.cond17.preheader

while.cond30.preheader:                           ; preds = %while.body20, %while.cond17.preheader
  %output.addr.0.lcssa = phi i8* [ %incdec.ptr3, %while.cond17.preheader ], [ %incdec.ptr25, %while.body20 ]
  %tobool32.not73 = icmp eq i32 %sub4.pre-phi, 0
  br i1 %tobool32.not73, label %while.end48, label %while.body33

while.body20:                                     ; preds = %while.body20.preheader, %while.body20
  %i.180 = phi i32 [ %dec18, %while.body20 ], [ %i.180.ph, %while.body20.preheader ]
  %v.179 = phi i8 [ %shl27, %while.body20 ], [ %v.179.ph, %while.body20.preheader ]
  %output.addr.078 = phi i8* [ %incdec.ptr25, %while.body20 ], [ %incdec.ptr3, %while.body20.preheader ]
  %dec18 = add nsw i32 %i.180, -1
  %tobool23.not = icmp sgt i8 %v.179, -1
  %conv24 = select i1 %tobool23.not, i8 48, i8 49
  %incdec.ptr25 = getelementptr inbounds i8, i8* %output.addr.078, i32 1
  store i8 %conv24, i8* %output.addr.078, align 1, !tbaa !7
  %shl27 = shl i8 %v.179, 1
  %tobool19.not = icmp eq i32 %dec18, 0
  br i1 %tobool19.not, label %while.cond30.preheader, label %while.body20

while.body33:                                     ; preds = %while.cond30.preheader, %while.body33
  %dec3176 = phi i32 [ %dec31, %while.body33 ], [ %sub4.pre-phi, %while.cond30.preheader ]
  %length.addr.175 = phi i32 [ %dec3176, %while.body33 ], [ %length.addr.0.lcssa, %while.cond30.preheader ]
  %output.addr.174 = phi i8* [ %incdec.ptr44.7, %while.body33 ], [ %output.addr.0.lcssa, %while.cond30.preheader ]
  %sub35 = add i32 %length.addr.175, -2
  %arrayidx36 = getelementptr inbounds i8, i8* %input, i32 %sub35
  %4 = load i8, i8* %arrayidx36, align 1, !tbaa !7
  %tobool41.not = icmp sgt i8 %4, -1
  %conv43 = select i1 %tobool41.not, i8 48, i8 49
  %incdec.ptr44 = getelementptr inbounds i8, i8* %output.addr.174, i32 1
  store i8 %conv43, i8* %output.addr.174, align 1, !tbaa !7
  %shl46.mask = and i8 %4, 64
  %tobool41.not.1 = icmp eq i8 %shl46.mask, 0
  %conv43.1 = select i1 %tobool41.not.1, i8 48, i8 49
  %incdec.ptr44.1 = getelementptr inbounds i8, i8* %output.addr.174, i32 2
  store i8 %conv43.1, i8* %incdec.ptr44, align 1, !tbaa !7
  %shl46.1.mask = and i8 %4, 32
  %tobool41.not.2 = icmp eq i8 %shl46.1.mask, 0
  %conv43.2 = select i1 %tobool41.not.2, i8 48, i8 49
  %incdec.ptr44.2 = getelementptr inbounds i8, i8* %output.addr.174, i32 3
  store i8 %conv43.2, i8* %incdec.ptr44.1, align 1, !tbaa !7
  %shl46.2.mask = and i8 %4, 16
  %tobool41.not.3 = icmp eq i8 %shl46.2.mask, 0
  %conv43.3 = select i1 %tobool41.not.3, i8 48, i8 49
  %incdec.ptr44.3 = getelementptr inbounds i8, i8* %output.addr.174, i32 4
  store i8 %conv43.3, i8* %incdec.ptr44.2, align 1, !tbaa !7
  %shl46.3.mask = and i8 %4, 8
  %tobool41.not.4 = icmp eq i8 %shl46.3.mask, 0
  %conv43.4 = select i1 %tobool41.not.4, i8 48, i8 49
  %incdec.ptr44.4 = getelementptr inbounds i8, i8* %output.addr.174, i32 5
  store i8 %conv43.4, i8* %incdec.ptr44.3, align 1, !tbaa !7
  %shl46.4.mask = and i8 %4, 4
  %tobool41.not.5 = icmp eq i8 %shl46.4.mask, 0
  %conv43.5 = select i1 %tobool41.not.5, i8 48, i8 49
  %incdec.ptr44.5 = getelementptr inbounds i8, i8* %output.addr.174, i32 6
  store i8 %conv43.5, i8* %incdec.ptr44.4, align 1, !tbaa !7
  %shl46.5.mask = and i8 %4, 2
  %tobool41.not.6 = icmp eq i8 %shl46.5.mask, 0
  %conv43.6 = select i1 %tobool41.not.6, i8 48, i8 49
  %incdec.ptr44.6 = getelementptr inbounds i8, i8* %output.addr.174, i32 7
  store i8 %conv43.6, i8* %incdec.ptr44.5, align 1, !tbaa !7
  %shl46.6.mask = and i8 %4, 1
  %5 = or i8 %shl46.6.mask, 48
  %incdec.ptr44.7 = getelementptr inbounds i8, i8* %output.addr.174, i32 8
  store i8 %5, i8* %incdec.ptr44.6, align 1, !tbaa !7
  %dec31 = add i32 %dec3176, -1
  %tobool32.not = icmp eq i32 %dec31, 0
  br i1 %tobool32.not, label %while.end48, label %while.body33

while.end48:                                      ; preds = %while.body33, %while.cond30.preheader
  %output.addr.1.lcssa = phi i8* [ %output.addr.0.lcssa, %while.cond30.preheader ], [ %incdec.ptr44.7, %while.body33 ]
  ret i8* %output.addr.1.lcssa
}

; Function Attrs: nounwind writeonly
define internal i8* @uint2dec(i8* %output, i64 %val) local_unnamed_addr #11 {
entry:
  %buf = alloca [20 x i8], align 16
  %0 = getelementptr inbounds [20 x i8], [20 x i8]* %buf, i32 0, i32 0
  call void @llvm.lifetime.start.p0i8(i64 20, i8* nonnull %0) #16
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %val.addr.0 = phi i64 [ %val, %entry ], [ %div, %do.body ]
  %len.0 = phi i32 [ 0, %entry ], [ %inc, %do.body ]
  %val.addr.0.frozen = freeze i64 %val.addr.0
  %div = udiv i64 %val.addr.0.frozen, 10
  %1 = mul i64 %div, 10
  %rem.decomposed = sub i64 %val.addr.0.frozen, %1
  %conv = trunc i64 %rem.decomposed to i8
  %inc = add nuw nsw i32 %len.0, 1
  %arrayidx = getelementptr inbounds [20 x i8], [20 x i8]* %buf, i32 0, i32 %len.0
  store i8 %conv, i8* %arrayidx, align 1, !tbaa !7
  %2 = icmp ult i64 %val.addr.0, 10
  br i1 %2, label %while.body.preheader, label %do.body

while.body.preheader:                             ; preds = %do.body
  %add14 = or i8 %conv, 48
  %incdec.ptr15 = getelementptr inbounds i8, i8* %output, i32 1
  store i8 %add14, i8* %output, align 1, !tbaa !7
  %tobool1.not17 = icmp eq i32 %len.0, 0
  br i1 %tobool1.not17, label %while.end, label %while.body.while.body_crit_edge

while.body.while.body_crit_edge:                  ; preds = %while.body.preheader, %while.body.while.body_crit_edge
  %dec19.in = phi i32 [ %dec19, %while.body.while.body_crit_edge ], [ %len.0, %while.body.preheader ]
  %incdec.ptr18 = phi i8* [ %incdec.ptr, %while.body.while.body_crit_edge ], [ %incdec.ptr15, %while.body.preheader ]
  %dec19 = add nsw i32 %dec19.in, -1
  %arrayidx2.phi.trans.insert = getelementptr inbounds [20 x i8], [20 x i8]* %buf, i32 0, i32 %dec19
  %.pre = load i8, i8* %arrayidx2.phi.trans.insert, align 1, !tbaa !7
  %add = add i8 %.pre, 48
  %incdec.ptr = getelementptr inbounds i8, i8* %incdec.ptr18, i32 1
  store i8 %add, i8* %incdec.ptr18, align 1, !tbaa !7
  %tobool1.not = icmp eq i32 %dec19, 0
  br i1 %tobool1.not, label %while.end, label %while.body.while.body_crit_edge

while.end:                                        ; preds = %while.body.while.body_crit_edge, %while.body.preheader
  %incdec.ptr.lcssa = phi i8* [ %incdec.ptr15, %while.body.preheader ], [ %incdec.ptr, %while.body.while.body_crit_edge ]
  call void @llvm.lifetime.end.p0i8(i64 20, i8* nonnull %0) #16
  ret i8* %incdec.ptr.lcssa
}

; Function Attrs: argmemonly nounwind willreturn
declare void @llvm.lifetime.start.p0i8(i64 immarg, i8* nocapture) #12

; Function Attrs: argmemonly nounwind willreturn
declare void @llvm.lifetime.end.p0i8(i64 immarg, i8* nocapture) #12

; Function Attrs: nounwind
define internal i8* @uint128dec(i8* %output, i128 %val128) local_unnamed_addr #2 {
entry:
  %val128.addr = alloca i128, align 16
  %divisor = alloca i128, align 16
  %q = alloca i128, align 16
  %r = alloca i128, align 16
  %buf = alloca [40 x i8], align 16
  store i128 %val128, i128* %val128.addr, align 16, !tbaa !17
  %0 = bitcast i128* %divisor to i8*
  call void @llvm.lifetime.start.p0i8(i64 16, i8* nonnull %0) #16
  store i128 10000000000000000000, i128* %divisor, align 16, !tbaa !17
  %1 = bitcast i128* %q to i8*
  call void @llvm.lifetime.start.p0i8(i64 16, i8* nonnull %1) #16
  %2 = bitcast i128* %r to i8*
  call void @llvm.lifetime.start.p0i8(i64 16, i8* nonnull %2) #16
  %3 = getelementptr inbounds [40 x i8], [40 x i8]* %buf, i32 0, i32 0
  call void @llvm.lifetime.start.p0i8(i64 40, i8* nonnull %3) #16
  %call = call i32 @udivmod128(i128* nonnull %val128.addr, i128* nonnull %divisor, i128* nonnull %r, i128* nonnull %q) #14
  %4 = load i128, i128* %r, align 16, !tbaa !17
  %conv = trunc i128 %4 to i64
  br label %do.body

do.body:                                          ; preds = %do.body, %entry
  %len.0 = phi i32 [ 0, %entry ], [ %inc, %do.body ]
  %val.0 = phi i64 [ %conv, %entry ], [ %div, %do.body ]
  %val.0.frozen = freeze i64 %val.0
  %div = udiv i64 %val.0.frozen, 10
  %5 = mul i64 %div, 10
  %rem.decomposed = sub i64 %val.0.frozen, %5
  %conv1 = trunc i64 %rem.decomposed to i8
  %inc = add nuw nsw i32 %len.0, 1
  %arrayidx = getelementptr inbounds [40 x i8], [40 x i8]* %buf, i32 0, i32 %len.0
  store i8 %conv1, i8* %arrayidx, align 1, !tbaa !7
  %6 = icmp ult i64 %val.0, 10
  br i1 %6, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  %call2 = call i32 @udivmod128(i128* nonnull %q, i128* nonnull %divisor, i128* nonnull %r, i128* nonnull %q) #14
  %7 = load i128, i128* %r, align 16, !tbaa !17
  %conv3 = trunc i128 %7 to i64
  %tobool4.not = icmp eq i64 %conv3, 0
  br i1 %tobool4.not, label %if.end, label %while.cond.preheader

while.cond.preheader:                             ; preds = %do.end
  %cmp73 = icmp ult i32 %len.0, 18
  br i1 %cmp73, label %while.body, label %do.body8.preheader

while.body:                                       ; preds = %while.cond.preheader, %while.body
  %len.174 = phi i32 [ %inc6, %while.body ], [ %inc, %while.cond.preheader ]
  %inc6 = add i32 %len.174, 1
  %arrayidx7 = getelementptr inbounds [40 x i8], [40 x i8]* %buf, i32 0, i32 %len.174
  store i8 0, i8* %arrayidx7, align 1, !tbaa !7
  %exitcond76.not = icmp eq i32 %inc6, 19
  br i1 %exitcond76.not, label %do.body8.preheader, label %while.body

do.body8.preheader:                               ; preds = %while.body, %while.cond.preheader
  %len.2.ph = phi i32 [ %inc, %while.cond.preheader ], [ 19, %while.body ]
  br label %do.body8

do.body8:                                         ; preds = %do.body8.preheader, %do.body8
  %len.2 = phi i32 [ %inc11, %do.body8 ], [ %len.2.ph, %do.body8.preheader ]
  %val.1 = phi i64 [ %div13, %do.body8 ], [ %conv3, %do.body8.preheader ]
  %val.1.frozen = freeze i64 %val.1
  %div13 = udiv i64 %val.1.frozen, 10
  %8 = mul i64 %div13, 10
  %rem9.decomposed = sub i64 %val.1.frozen, %8
  %conv10 = trunc i64 %rem9.decomposed to i8
  %inc11 = add nuw nsw i32 %len.2, 1
  %arrayidx12 = getelementptr inbounds [40 x i8], [40 x i8]* %buf, i32 0, i32 %len.2
  store i8 %conv10, i8* %arrayidx12, align 1, !tbaa !7
  %9 = icmp ult i64 %val.1, 10
  br i1 %9, label %if.end, label %do.body8

if.end:                                           ; preds = %do.body8, %do.end
  %len.3 = phi i32 [ %inc, %do.end ], [ %inc11, %do.body8 ]
  %10 = load i128, i128* %q, align 16, !tbaa !17
  %conv17 = trunc i128 %10 to i64
  %tobool18.not = icmp eq i64 %conv17, 0
  br i1 %tobool18.not, label %while.body39.preheader, label %while.cond20.preheader

while.cond20.preheader:                           ; preds = %if.end
  %cmp2170 = icmp slt i32 %len.3, 38
  br i1 %cmp2170, label %while.body23, label %do.body27.preheader

while.body23:                                     ; preds = %while.cond20.preheader, %while.body23
  %len.471 = phi i32 [ %inc24, %while.body23 ], [ %len.3, %while.cond20.preheader ]
  %inc24 = add i32 %len.471, 1
  %arrayidx25 = getelementptr inbounds [40 x i8], [40 x i8]* %buf, i32 0, i32 %len.471
  store i8 0, i8* %arrayidx25, align 1, !tbaa !7
  %exitcond.not = icmp eq i32 %inc24, 38
  br i1 %exitcond.not, label %do.body27.preheader, label %while.body23

do.body27.preheader:                              ; preds = %while.body23, %while.cond20.preheader
  %len.5.ph = phi i32 [ %len.3, %while.cond20.preheader ], [ 38, %while.body23 ]
  br label %do.body27

do.body27:                                        ; preds = %do.body27.preheader, %do.body27
  %len.5 = phi i32 [ %inc30, %do.body27 ], [ %len.5.ph, %do.body27.preheader ]
  %val.2 = phi i64 [ %div32, %do.body27 ], [ %conv17, %do.body27.preheader ]
  %val.2.frozen = freeze i64 %val.2
  %div32 = udiv i64 %val.2.frozen, 10
  %11 = mul i64 %div32, 10
  %rem28.decomposed = sub i64 %val.2.frozen, %11
  %conv29 = trunc i64 %rem28.decomposed to i8
  %inc30 = add nsw i32 %len.5, 1
  %arrayidx31 = getelementptr inbounds [40 x i8], [40 x i8]* %buf, i32 0, i32 %len.5
  store i8 %conv29, i8* %arrayidx31, align 1, !tbaa !7
  %12 = icmp ult i64 %val.2, 10
  br i1 %12, label %if.end36, label %do.body27

if.end36:                                         ; preds = %do.body27
  %tobool38.not67 = icmp eq i32 %inc30, 0
  br i1 %tobool38.not67, label %while.end43, label %while.body39.preheader

while.body39.preheader:                           ; preds = %if.end, %if.end36
  %dec69.in.ph = phi i32 [ %len.3, %if.end ], [ %inc30, %if.end36 ]
  br label %while.body39

while.body39:                                     ; preds = %while.body39.preheader, %while.body39
  %dec69.in = phi i32 [ %dec69, %while.body39 ], [ %dec69.in.ph, %while.body39.preheader ]
  %output.addr.068 = phi i8* [ %incdec.ptr, %while.body39 ], [ %output, %while.body39.preheader ]
  %dec69 = add nsw i32 %dec69.in, -1
  %arrayidx40 = getelementptr inbounds [40 x i8], [40 x i8]* %buf, i32 0, i32 %dec69
  %13 = load i8, i8* %arrayidx40, align 1, !tbaa !7
  %add = add i8 %13, 48
  %incdec.ptr = getelementptr inbounds i8, i8* %output.addr.068, i32 1
  store i8 %add, i8* %output.addr.068, align 1, !tbaa !7
  %tobool38.not = icmp eq i32 %dec69, 0
  br i1 %tobool38.not, label %while.end43, label %while.body39

while.end43:                                      ; preds = %while.body39, %if.end36
  %output.addr.0.lcssa = phi i8* [ %output, %if.end36 ], [ %incdec.ptr, %while.body39 ]
  call void @llvm.lifetime.end.p0i8(i64 40, i8* nonnull %3) #16
  call void @llvm.lifetime.end.p0i8(i64 16, i8* nonnull %2) #16
  call void @llvm.lifetime.end.p0i8(i64 16, i8* nonnull %1) #16
  call void @llvm.lifetime.end.p0i8(i64 16, i8* nonnull %0) #16
  ret i8* %output.addr.0.lcssa
}

; Function Attrs: nounwind
define internal i8* @uint256dec(i8* %output, i256* nocapture readonly %val256) local_unnamed_addr #2 {
do.body.preheader:
  %divisor = alloca i256, align 8
  %q = alloca i256, align 8
  %r = alloca i256, align 8
  %buf = alloca [80 x i8], align 16
  %0 = bitcast i256* %divisor to i8*
  call void @llvm.lifetime.start.p0i8(i64 32, i8* nonnull %0) #16
  store i256 10000000000000000000, i256* %divisor, align 8, !tbaa !19
  %1 = bitcast i256* %q to i8*
  call void @llvm.lifetime.start.p0i8(i64 32, i8* nonnull %1) #16
  %2 = load i256, i256* %val256, align 8, !tbaa !19
  store i256 %2, i256* %q, align 8, !tbaa !19
  %3 = bitcast i256* %r to i8*
  call void @llvm.lifetime.start.p0i8(i64 32, i8* nonnull %3) #16
  %4 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 0
  call void @llvm.lifetime.start.p0i8(i64 80, i8* nonnull %4) #16
  %call = call i32 @udivmod256(i256* nonnull %q, i256* nonnull %divisor, i256* nonnull %r, i256* nonnull %q) #14
  %5 = load i256, i256* %r, align 8, !tbaa !19
  %conv = trunc i256 %5 to i64
  br label %do.body

for.cond:                                         ; preds = %do.end
  %call.1 = call i32 @udivmod256(i256* nonnull %q, i256* nonnull %divisor, i256* nonnull %r, i256* nonnull %q) #14
  %6 = load i256, i256* %r, align 8, !tbaa !19
  %conv.1 = trunc i256 %6 to i64
  %cmp162.1 = icmp ult i32 %len.2, 18
  br i1 %cmp162.1, label %while.body.1, label %do.body.1.preheader

do.body:                                          ; preds = %do.body.preheader, %do.body
  %len.2 = phi i32 [ %inc4, %do.body ], [ 0, %do.body.preheader ]
  %val.0 = phi i64 [ %div, %do.body ], [ %conv, %do.body.preheader ]
  %val.0.frozen = freeze i64 %val.0
  %div = udiv i64 %val.0.frozen, 10
  %7 = mul i64 %div, 10
  %rem.decomposed = sub i64 %val.0.frozen, %7
  %conv3 = trunc i64 %rem.decomposed to i8
  %inc4 = add nuw nsw i32 %len.2, 1
  %arrayidx5 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %len.2
  store i8 %conv3, i8* %arrayidx5, align 1, !tbaa !7
  %8 = icmp ult i64 %val.0, 10
  br i1 %8, label %do.end, label %do.body

do.end:                                           ; preds = %do.body
  %9 = load i256, i256* %q, align 8, !tbaa !19
  %cmp6.not = icmp eq i256 %9, 0
  br i1 %cmp6.not, label %while.body25.preheader, label %for.cond

cleanup8:                                         ; preds = %do.end.3
  %extract.t = trunc i256 %23 to i64
  %tobool11.not = icmp eq i64 %extract.t, 0
  br i1 %tobool11.not, label %if.end22, label %do.body13

do.body13:                                        ; preds = %cleanup8, %do.body13
  %len.4 = phi i32 [ %inc16, %do.body13 ], [ %inc4.3, %cleanup8 ]
  %val9.0 = phi i64 [ %div18, %do.body13 ], [ %extract.t, %cleanup8 ]
  %val9.0.frozen = freeze i64 %val9.0
  %div18 = udiv i64 %val9.0.frozen, 10
  %10 = mul i64 %div18, 10
  %rem14.decomposed = sub i64 %val9.0.frozen, %10
  %conv15 = trunc i64 %rem14.decomposed to i8
  %inc16 = add nsw i32 %len.4, 1
  %arrayidx17 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %len.4
  store i8 %conv15, i8* %arrayidx17, align 1, !tbaa !7
  %11 = icmp ult i64 %val9.0, 10
  br i1 %11, label %if.end22, label %do.body13

if.end22:                                         ; preds = %do.body13, %do.end.3, %do.end.2, %do.end.1, %cleanup8
  %len.5 = phi i32 [ %inc4.3, %cleanup8 ], [ %inc4.3, %do.end.3 ], [ %inc4.2, %do.end.2 ], [ %inc4.1, %do.end.1 ], [ %inc16, %do.body13 ]
  %tobool24.not59 = icmp eq i32 %len.5, 0
  br i1 %tobool24.not59, label %while.end30, label %while.body25.preheader

while.body25.preheader:                           ; preds = %do.end, %if.end22
  %dec61.in.ph = phi i32 [ %inc4, %do.end ], [ %len.5, %if.end22 ]
  br label %while.body25

while.body25:                                     ; preds = %while.body25.preheader, %while.body25
  %dec61.in = phi i32 [ %dec61, %while.body25 ], [ %dec61.in.ph, %while.body25.preheader ]
  %output.addr.060 = phi i8* [ %incdec.ptr, %while.body25 ], [ %output, %while.body25.preheader ]
  %dec61 = add nsw i32 %dec61.in, -1
  %arrayidx26 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %dec61
  %12 = load i8, i8* %arrayidx26, align 1, !tbaa !7
  %add28 = add i8 %12, 48
  %incdec.ptr = getelementptr inbounds i8, i8* %output.addr.060, i32 1
  store i8 %add28, i8* %output.addr.060, align 1, !tbaa !7
  %tobool24.not = icmp eq i32 %dec61, 0
  br i1 %tobool24.not, label %while.end30, label %while.body25

while.end30:                                      ; preds = %while.body25, %if.end22
  %output.addr.0.lcssa = phi i8* [ %output, %if.end22 ], [ %incdec.ptr, %while.body25 ]
  call void @llvm.lifetime.end.p0i8(i64 80, i8* nonnull %4) #16
  call void @llvm.lifetime.end.p0i8(i64 32, i8* nonnull %3) #16
  call void @llvm.lifetime.end.p0i8(i64 32, i8* nonnull %1) #16
  call void @llvm.lifetime.end.p0i8(i64 32, i8* nonnull %0) #16
  ret i8* %output.addr.0.lcssa

while.body.1:                                     ; preds = %for.cond, %while.body.1
  %len.163.1 = phi i32 [ %inc.1, %while.body.1 ], [ %inc4, %for.cond ]
  %inc.1 = add nuw nsw i32 %len.163.1, 1
  %arrayidx.1 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %len.163.1
  store i8 0, i8* %arrayidx.1, align 1, !tbaa !7
  %exitcond.1.not = icmp eq i32 %inc.1, 19
  br i1 %exitcond.1.not, label %do.body.1.preheader, label %while.body.1

do.body.1.preheader:                              ; preds = %while.body.1, %for.cond
  %len.2.1.ph = phi i32 [ %inc4, %for.cond ], [ 19, %while.body.1 ]
  br label %do.body.1

do.body.1:                                        ; preds = %do.body.1.preheader, %do.body.1
  %len.2.1 = phi i32 [ %inc4.1, %do.body.1 ], [ %len.2.1.ph, %do.body.1.preheader ]
  %val.0.1 = phi i64 [ %div.1, %do.body.1 ], [ %conv.1, %do.body.1.preheader ]
  %val.0.1.frozen = freeze i64 %val.0.1
  %div.1 = udiv i64 %val.0.1.frozen, 10
  %13 = mul i64 %div.1, 10
  %rem.1.decomposed = sub i64 %val.0.1.frozen, %13
  %conv3.1 = trunc i64 %rem.1.decomposed to i8
  %inc4.1 = add nsw i32 %len.2.1, 1
  %arrayidx5.1 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %len.2.1
  store i8 %conv3.1, i8* %arrayidx5.1, align 1, !tbaa !7
  %14 = icmp ult i64 %val.0.1, 10
  br i1 %14, label %do.end.1, label %do.body.1

do.end.1:                                         ; preds = %do.body.1
  %15 = load i256, i256* %q, align 8, !tbaa !19
  %cmp6.not.1 = icmp eq i256 %15, 0
  br i1 %cmp6.not.1, label %if.end22, label %for.cond.1

for.cond.1:                                       ; preds = %do.end.1
  %call.2 = call i32 @udivmod256(i256* nonnull %q, i256* nonnull %divisor, i256* nonnull %r, i256* nonnull %q) #14
  %16 = load i256, i256* %r, align 8, !tbaa !19
  %conv.2 = trunc i256 %16 to i64
  %cmp162.2 = icmp slt i32 %len.2.1, 37
  br i1 %cmp162.2, label %while.body.2, label %do.body.2.preheader

while.body.2:                                     ; preds = %for.cond.1, %while.body.2
  %len.163.2 = phi i32 [ %inc.2, %while.body.2 ], [ %inc4.1, %for.cond.1 ]
  %inc.2 = add nsw i32 %len.163.2, 1
  %arrayidx.2 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %len.163.2
  store i8 0, i8* %arrayidx.2, align 1, !tbaa !7
  %exitcond.2.not = icmp eq i32 %inc.2, 38
  br i1 %exitcond.2.not, label %do.body.2.preheader, label %while.body.2

do.body.2.preheader:                              ; preds = %while.body.2, %for.cond.1
  %len.2.2.ph = phi i32 [ %inc4.1, %for.cond.1 ], [ 38, %while.body.2 ]
  br label %do.body.2

do.body.2:                                        ; preds = %do.body.2.preheader, %do.body.2
  %len.2.2 = phi i32 [ %inc4.2, %do.body.2 ], [ %len.2.2.ph, %do.body.2.preheader ]
  %val.0.2 = phi i64 [ %div.2, %do.body.2 ], [ %conv.2, %do.body.2.preheader ]
  %val.0.2.frozen = freeze i64 %val.0.2
  %div.2 = udiv i64 %val.0.2.frozen, 10
  %17 = mul i64 %div.2, 10
  %rem.2.decomposed = sub i64 %val.0.2.frozen, %17
  %conv3.2 = trunc i64 %rem.2.decomposed to i8
  %inc4.2 = add nsw i32 %len.2.2, 1
  %arrayidx5.2 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %len.2.2
  store i8 %conv3.2, i8* %arrayidx5.2, align 1, !tbaa !7
  %18 = icmp ult i64 %val.0.2, 10
  br i1 %18, label %do.end.2, label %do.body.2

do.end.2:                                         ; preds = %do.body.2
  %19 = load i256, i256* %q, align 8, !tbaa !19
  %cmp6.not.2 = icmp eq i256 %19, 0
  br i1 %cmp6.not.2, label %if.end22, label %for.cond.2

for.cond.2:                                       ; preds = %do.end.2
  %call.3 = call i32 @udivmod256(i256* nonnull %q, i256* nonnull %divisor, i256* nonnull %r, i256* nonnull %q) #14
  %20 = load i256, i256* %r, align 8, !tbaa !19
  %conv.3 = trunc i256 %20 to i64
  %cmp162.3 = icmp slt i32 %len.2.2, 56
  br i1 %cmp162.3, label %while.body.3, label %do.body.3.preheader

while.body.3:                                     ; preds = %for.cond.2, %while.body.3
  %len.163.3 = phi i32 [ %inc.3, %while.body.3 ], [ %inc4.2, %for.cond.2 ]
  %inc.3 = add nsw i32 %len.163.3, 1
  %arrayidx.3 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %len.163.3
  store i8 0, i8* %arrayidx.3, align 1, !tbaa !7
  %exitcond.3.not = icmp eq i32 %inc.3, 57
  br i1 %exitcond.3.not, label %do.body.3.preheader, label %while.body.3

do.body.3.preheader:                              ; preds = %while.body.3, %for.cond.2
  %len.2.3.ph = phi i32 [ %inc4.2, %for.cond.2 ], [ 57, %while.body.3 ]
  br label %do.body.3

do.body.3:                                        ; preds = %do.body.3.preheader, %do.body.3
  %len.2.3 = phi i32 [ %inc4.3, %do.body.3 ], [ %len.2.3.ph, %do.body.3.preheader ]
  %val.0.3 = phi i64 [ %div.3, %do.body.3 ], [ %conv.3, %do.body.3.preheader ]
  %val.0.3.frozen = freeze i64 %val.0.3
  %div.3 = udiv i64 %val.0.3.frozen, 10
  %21 = mul i64 %div.3, 10
  %rem.3.decomposed = sub i64 %val.0.3.frozen, %21
  %conv3.3 = trunc i64 %rem.3.decomposed to i8
  %inc4.3 = add nsw i32 %len.2.3, 1
  %arrayidx5.3 = getelementptr inbounds [80 x i8], [80 x i8]* %buf, i32 0, i32 %len.2.3
  store i8 %conv3.3, i8* %arrayidx5.3, align 1, !tbaa !7
  %22 = icmp ult i64 %val.0.3, 10
  br i1 %22, label %do.end.3, label %do.body.3

do.end.3:                                         ; preds = %do.body.3
  %23 = load i256, i256* %q, align 8, !tbaa !19
  %cmp6.not.3 = icmp eq i256 %23, 0
  br i1 %cmp6.not.3, label %if.end22, label %cleanup8
}

; Function Attrs: norecurse nounwind readnone
define internal zeroext i8 @getConstant(i8 zeroext %type, i8 zeroext %index) local_unnamed_addr #10 {
entry:
  %conv = zext i8 %type to i32
  %conv1 = zext i8 %index to i32
  %add = add nuw nsw i32 %conv1, %conv
  %arrayidx = getelementptr inbounds [72 x i8], [72 x i8]* @constants, i32 0, i32 %add
  %0 = load i8, i8* %arrayidx, align 1, !tbaa !7
  ret i8 %0
}

; Function Attrs: nounwind
define internal void @keccak_init(%struct.SHA3_CTX* %ctx) local_unnamed_addr #2 {
entry:
  %0 = bitcast %struct.SHA3_CTX* %ctx to i8*
  tail call void @__memset(i8* %0, i8 zeroext 0, i32 400) #14
  ret void
}

; Function Attrs: nounwind
define internal void @keccak_update(%struct.SHA3_CTX* %ctx, i8* %msg, i16 zeroext %size) local_unnamed_addr #2 {
entry:
  %rest = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 2
  %0 = load i16, i16* %rest, align 8, !tbaa !23
  %conv = zext i16 %0 to i32
  %conv2 = zext i16 %size to i32
  %add = add nuw nsw i32 %conv, %conv2
  %rem = urem i32 %add, 136
  %conv3 = trunc i32 %rem to i16
  store i16 %conv3, i16* %rest, align 8, !tbaa !23
  %tobool.not = icmp eq i16 %0, 0
  br i1 %tobool.not, label %if.end27, label %if.then

if.then:                                          ; preds = %entry
  %sub = sub i16 136, %0
  %arraydecay = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 1, i32 0
  %1 = bitcast i64* %arraydecay to i8*
  %add.ptr = getelementptr inbounds i8, i8* %1, i32 %conv
  %cmp = icmp ugt i16 %sub, %size
  %2 = icmp ult i16 %sub, %size
  %cond87 = select i1 %2, i16 %sub, i16 %size
  %3 = zext i16 %cond87 to i32
  tail call void @__memcpy(i8* nonnull %add.ptr, i8* %msg, i32 %3) #14
  br i1 %cmp, label %cleanup51, label %cleanup.thread

cleanup.thread:                                   ; preds = %if.then
  %conv9 = zext i16 %sub to i32
  %arraydecay18 = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 0, i32 0
  tail call fastcc void @sha3_process_block(i64* %arraydecay18, i64* nonnull %arraydecay) #15
  %add.ptr22 = getelementptr inbounds i8, i8* %msg, i32 %conv9
  %sub25 = sub i16 %size, %sub
  br label %if.end27

if.end27:                                         ; preds = %cleanup.thread, %entry
  %size.addr.1 = phi i16 [ %size, %entry ], [ %sub25, %cleanup.thread ]
  %msg.addr.1 = phi i8* [ %msg, %entry ], [ %add.ptr22, %cleanup.thread ]
  %cmp2992 = icmp ugt i16 %size.addr.1, 135
  br i1 %cmp2992, label %while.body.lr.ph, label %while.end

while.body.lr.ph:                                 ; preds = %if.end27
  %arraydecay35 = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 1, i32 0
  %4 = bitcast i64* %arraydecay35 to i8*
  %arraydecay40 = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 0, i32 0
  br label %while.body

while.body:                                       ; preds = %while.body.lr.ph, %if.end38
  %msg.addr.294 = phi i8* [ %msg.addr.1, %while.body.lr.ph ], [ %add.ptr41, %if.end38 ]
  %size.addr.293 = phi i16 [ %size.addr.1, %while.body.lr.ph ], [ %sub43, %if.end38 ]
  %sub.ptr.lhs.cast = ptrtoint i8* %msg.addr.294 to i32
  %and = and i32 %sub.ptr.lhs.cast, 7
  %cmp31 = icmp eq i32 %and, 0
  br i1 %cmp31, label %if.then33, label %if.else

if.then33:                                        ; preds = %while.body
  %5 = bitcast i8* %msg.addr.294 to i64*
  br label %if.end38

if.else:                                          ; preds = %while.body
  tail call void @__memcpy(i8* nonnull %4, i8* %msg.addr.294, i32 136) #14
  br label %if.end38

if.end38:                                         ; preds = %if.else, %if.then33
  %aligned_message_block.0 = phi i64* [ %5, %if.then33 ], [ %arraydecay35, %if.else ]
  tail call fastcc void @sha3_process_block(i64* %arraydecay40, i64* %aligned_message_block.0) #15
  %add.ptr41 = getelementptr inbounds i8, i8* %msg.addr.294, i32 136
  %sub43 = add i16 %size.addr.293, -136
  %cmp29 = icmp ugt i16 %sub43, 135
  br i1 %cmp29, label %while.body, label %while.end

while.end:                                        ; preds = %if.end38, %if.end27
  %size.addr.2.lcssa = phi i16 [ %size.addr.1, %if.end27 ], [ %sub43, %if.end38 ]
  %msg.addr.2.lcssa = phi i8* [ %msg.addr.1, %if.end27 ], [ %add.ptr41, %if.end38 ]
  %tobool45.not = icmp eq i16 %size.addr.2.lcssa, 0
  br i1 %tobool45.not, label %cleanup51, label %if.then46

if.then46:                                        ; preds = %while.end
  %conv28.lcssa = zext i16 %size.addr.2.lcssa to i32
  %arraydecay48 = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 1, i32 0
  %6 = bitcast i64* %arraydecay48 to i8*
  tail call void @__memcpy(i8* nonnull %6, i8* %msg.addr.2.lcssa, i32 %conv28.lcssa) #14
  br label %cleanup51

cleanup51:                                        ; preds = %if.then, %if.then46, %while.end
  ret void
}

; Function Attrs: nofree norecurse nounwind
define internal fastcc void @sha3_process_block(i64* %hash, i64* nocapture readonly %block) unnamed_addr #1 {
entry:
  %0 = load i64, i64* %block, align 8, !tbaa !3
  %1 = load i64, i64* %hash, align 8, !tbaa !3
  %xor = xor i64 %1, %0
  store i64 %xor, i64* %hash, align 8, !tbaa !3
  %arrayidx.1 = getelementptr inbounds i64, i64* %block, i32 1
  %2 = load i64, i64* %arrayidx.1, align 8, !tbaa !3
  %arrayidx3.1 = getelementptr inbounds i64, i64* %hash, i32 1
  %3 = load i64, i64* %arrayidx3.1, align 8, !tbaa !3
  %xor.1 = xor i64 %3, %2
  store i64 %xor.1, i64* %arrayidx3.1, align 8, !tbaa !3
  %arrayidx.2 = getelementptr inbounds i64, i64* %block, i32 2
  %4 = load i64, i64* %arrayidx.2, align 8, !tbaa !3
  %arrayidx3.2 = getelementptr inbounds i64, i64* %hash, i32 2
  %5 = load i64, i64* %arrayidx3.2, align 8, !tbaa !3
  %xor.2 = xor i64 %5, %4
  store i64 %xor.2, i64* %arrayidx3.2, align 8, !tbaa !3
  %arrayidx.3 = getelementptr inbounds i64, i64* %block, i32 3
  %6 = load i64, i64* %arrayidx.3, align 8, !tbaa !3
  %arrayidx3.3 = getelementptr inbounds i64, i64* %hash, i32 3
  %7 = load i64, i64* %arrayidx3.3, align 8, !tbaa !3
  %xor.3 = xor i64 %7, %6
  store i64 %xor.3, i64* %arrayidx3.3, align 8, !tbaa !3
  %arrayidx.4 = getelementptr inbounds i64, i64* %block, i32 4
  %8 = load i64, i64* %arrayidx.4, align 8, !tbaa !3
  %arrayidx3.4 = getelementptr inbounds i64, i64* %hash, i32 4
  %9 = load i64, i64* %arrayidx3.4, align 8, !tbaa !3
  %xor.4 = xor i64 %9, %8
  store i64 %xor.4, i64* %arrayidx3.4, align 8, !tbaa !3
  %arrayidx.5 = getelementptr inbounds i64, i64* %block, i32 5
  %10 = load i64, i64* %arrayidx.5, align 8, !tbaa !3
  %arrayidx3.5 = getelementptr inbounds i64, i64* %hash, i32 5
  %11 = load i64, i64* %arrayidx3.5, align 8, !tbaa !3
  %xor.5 = xor i64 %11, %10
  store i64 %xor.5, i64* %arrayidx3.5, align 8, !tbaa !3
  %arrayidx.6 = getelementptr inbounds i64, i64* %block, i32 6
  %12 = load i64, i64* %arrayidx.6, align 8, !tbaa !3
  %arrayidx3.6 = getelementptr inbounds i64, i64* %hash, i32 6
  %13 = load i64, i64* %arrayidx3.6, align 8, !tbaa !3
  %xor.6 = xor i64 %13, %12
  store i64 %xor.6, i64* %arrayidx3.6, align 8, !tbaa !3
  %arrayidx.7 = getelementptr inbounds i64, i64* %block, i32 7
  %14 = load i64, i64* %arrayidx.7, align 8, !tbaa !3
  %arrayidx3.7 = getelementptr inbounds i64, i64* %hash, i32 7
  %15 = load i64, i64* %arrayidx3.7, align 8, !tbaa !3
  %xor.7 = xor i64 %15, %14
  store i64 %xor.7, i64* %arrayidx3.7, align 8, !tbaa !3
  %arrayidx.8 = getelementptr inbounds i64, i64* %block, i32 8
  %16 = load i64, i64* %arrayidx.8, align 8, !tbaa !3
  %arrayidx3.8 = getelementptr inbounds i64, i64* %hash, i32 8
  %17 = load i64, i64* %arrayidx3.8, align 8, !tbaa !3
  %xor.8 = xor i64 %17, %16
  store i64 %xor.8, i64* %arrayidx3.8, align 8, !tbaa !3
  %arrayidx.9 = getelementptr inbounds i64, i64* %block, i32 9
  %18 = load i64, i64* %arrayidx.9, align 8, !tbaa !3
  %arrayidx3.9 = getelementptr inbounds i64, i64* %hash, i32 9
  %19 = load i64, i64* %arrayidx3.9, align 8, !tbaa !3
  %xor.9 = xor i64 %19, %18
  store i64 %xor.9, i64* %arrayidx3.9, align 8, !tbaa !3
  %arrayidx.10 = getelementptr inbounds i64, i64* %block, i32 10
  %20 = load i64, i64* %arrayidx.10, align 8, !tbaa !3
  %arrayidx3.10 = getelementptr inbounds i64, i64* %hash, i32 10
  %21 = load i64, i64* %arrayidx3.10, align 8, !tbaa !3
  %xor.10 = xor i64 %21, %20
  store i64 %xor.10, i64* %arrayidx3.10, align 8, !tbaa !3
  %arrayidx.11 = getelementptr inbounds i64, i64* %block, i32 11
  %22 = load i64, i64* %arrayidx.11, align 8, !tbaa !3
  %arrayidx3.11 = getelementptr inbounds i64, i64* %hash, i32 11
  %23 = load i64, i64* %arrayidx3.11, align 8, !tbaa !3
  %xor.11 = xor i64 %23, %22
  store i64 %xor.11, i64* %arrayidx3.11, align 8, !tbaa !3
  %arrayidx.12 = getelementptr inbounds i64, i64* %block, i32 12
  %24 = load i64, i64* %arrayidx.12, align 8, !tbaa !3
  %arrayidx3.12 = getelementptr inbounds i64, i64* %hash, i32 12
  %25 = load i64, i64* %arrayidx3.12, align 8, !tbaa !3
  %xor.12 = xor i64 %25, %24
  store i64 %xor.12, i64* %arrayidx3.12, align 8, !tbaa !3
  %arrayidx.13 = getelementptr inbounds i64, i64* %block, i32 13
  %26 = load i64, i64* %arrayidx.13, align 8, !tbaa !3
  %arrayidx3.13 = getelementptr inbounds i64, i64* %hash, i32 13
  %27 = load i64, i64* %arrayidx3.13, align 8, !tbaa !3
  %xor.13 = xor i64 %27, %26
  store i64 %xor.13, i64* %arrayidx3.13, align 8, !tbaa !3
  %arrayidx.14 = getelementptr inbounds i64, i64* %block, i32 14
  %28 = load i64, i64* %arrayidx.14, align 8, !tbaa !3
  %arrayidx3.14 = getelementptr inbounds i64, i64* %hash, i32 14
  %29 = load i64, i64* %arrayidx3.14, align 8, !tbaa !3
  %xor.14 = xor i64 %29, %28
  store i64 %xor.14, i64* %arrayidx3.14, align 8, !tbaa !3
  %arrayidx.15 = getelementptr inbounds i64, i64* %block, i32 15
  %30 = load i64, i64* %arrayidx.15, align 8, !tbaa !3
  %arrayidx3.15 = getelementptr inbounds i64, i64* %hash, i32 15
  %31 = load i64, i64* %arrayidx3.15, align 8, !tbaa !3
  %xor.15 = xor i64 %31, %30
  store i64 %xor.15, i64* %arrayidx3.15, align 8, !tbaa !3
  %arrayidx.16 = getelementptr inbounds i64, i64* %block, i32 16
  %32 = load i64, i64* %arrayidx.16, align 8, !tbaa !3
  %arrayidx3.16 = getelementptr inbounds i64, i64* %hash, i32 16
  %33 = load i64, i64* %arrayidx3.16, align 8, !tbaa !3
  %xor.16 = xor i64 %33, %32
  store i64 %xor.16, i64* %arrayidx3.16, align 8, !tbaa !3
  %arrayidx12.3.i.i = getelementptr inbounds i64, i64* %hash, i32 20
  %arrayidx12.3.1.i.i = getelementptr inbounds i64, i64* %hash, i32 21
  %arrayidx12.2.2.i.i = getelementptr inbounds i64, i64* %hash, i32 17
  %arrayidx12.3.2.i.i = getelementptr inbounds i64, i64* %hash, i32 22
  %arrayidx12.2.3.i.i = getelementptr inbounds i64, i64* %hash, i32 18
  %arrayidx12.3.3.i.i = getelementptr inbounds i64, i64* %hash, i32 23
  %arrayidx12.2.4.i.i = getelementptr inbounds i64, i64* %hash, i32 19
  %arrayidx12.3.4.i.i = getelementptr inbounds i64, i64* %hash, i32 24
  %.pre.i = load i64, i64* %hash, align 8, !tbaa !3
  %.pre63.i = load i64, i64* %arrayidx12.3.i.i, align 8, !tbaa !3
  %.pre66.i = load i64, i64* %arrayidx12.3.1.i.i, align 8, !tbaa !3
  %.pre69.i = load i64, i64* %arrayidx12.2.2.i.i, align 8, !tbaa !3
  %.pre70.i = load i64, i64* %arrayidx12.3.2.i.i, align 8, !tbaa !3
  %.pre73.i = load i64, i64* %arrayidx12.2.3.i.i, align 8, !tbaa !3
  %.pre74.i = load i64, i64* %arrayidx12.3.3.i.i, align 8, !tbaa !3
  %.pre77.i = load i64, i64* %arrayidx12.2.4.i.i, align 8, !tbaa !3
  %.pre78.i = load i64, i64* %arrayidx12.3.4.i.i, align 8, !tbaa !3
  %arrayidx.phi.trans.insert.i.phi.trans.insert = getelementptr inbounds i64, i64* %hash, i32 3
  %arrayidx.phi.trans.insert.i20 = getelementptr inbounds i64, i64* %hash, i32 2
  %arrayidx.phi.trans.insert.i = getelementptr inbounds i64, i64* %hash, i32 3
  %arrayidx.phi.trans.insert.i.phi.trans.insert.1 = getelementptr inbounds i64, i64* %hash, i32 4
  %arrayidx.phi.trans.insert.i.1 = getelementptr inbounds i64, i64* %hash, i32 4
  %arrayidx.phi.trans.insert.i.phi.trans.insert.2 = getelementptr inbounds i64, i64* %hash, i32 5
  %arrayidx.phi.trans.insert.i.2 = getelementptr inbounds i64, i64* %hash, i32 5
  %arrayidx.phi.trans.insert.i.phi.trans.insert.3 = getelementptr inbounds i64, i64* %hash, i32 6
  %arrayidx.phi.trans.insert.i.3 = getelementptr inbounds i64, i64* %hash, i32 6
  %arrayidx.phi.trans.insert.i.phi.trans.insert.4 = getelementptr inbounds i64, i64* %hash, i32 7
  %arrayidx.phi.trans.insert.i.4 = getelementptr inbounds i64, i64* %hash, i32 7
  %arrayidx.phi.trans.insert.i.phi.trans.insert.5 = getelementptr inbounds i64, i64* %hash, i32 8
  %arrayidx.phi.trans.insert.i.5 = getelementptr inbounds i64, i64* %hash, i32 8
  %arrayidx.phi.trans.insert.i.phi.trans.insert.6 = getelementptr inbounds i64, i64* %hash, i32 9
  %arrayidx.phi.trans.insert.i.6 = getelementptr inbounds i64, i64* %hash, i32 9
  %arrayidx.phi.trans.insert.i.phi.trans.insert.7 = getelementptr inbounds i64, i64* %hash, i32 10
  %arrayidx.phi.trans.insert.i.7 = getelementptr inbounds i64, i64* %hash, i32 10
  %arrayidx.phi.trans.insert.i.phi.trans.insert.8 = getelementptr inbounds i64, i64* %hash, i32 11
  %arrayidx.phi.trans.insert.i.8 = getelementptr inbounds i64, i64* %hash, i32 11
  %arrayidx.phi.trans.insert.i.phi.trans.insert.9 = getelementptr inbounds i64, i64* %hash, i32 12
  %arrayidx.phi.trans.insert.i.9 = getelementptr inbounds i64, i64* %hash, i32 12
  %arrayidx.phi.trans.insert.i.phi.trans.insert.10 = getelementptr inbounds i64, i64* %hash, i32 13
  %arrayidx.phi.trans.insert.i.10 = getelementptr inbounds i64, i64* %hash, i32 13
  %arrayidx.phi.trans.insert.i.phi.trans.insert.11 = getelementptr inbounds i64, i64* %hash, i32 14
  %arrayidx.phi.trans.insert.i.11 = getelementptr inbounds i64, i64* %hash, i32 14
  %arrayidx.phi.trans.insert.i.phi.trans.insert.12 = getelementptr inbounds i64, i64* %hash, i32 15
  %arrayidx.phi.trans.insert.i.12 = getelementptr inbounds i64, i64* %hash, i32 15
  %arrayidx.phi.trans.insert.i.phi.trans.insert.13 = getelementptr inbounds i64, i64* %hash, i32 16
  %arrayidx.phi.trans.insert.i.13 = getelementptr inbounds i64, i64* %hash, i32 16
  %arrayidx.phi.trans.insert.i.phi.trans.insert.14 = getelementptr inbounds i64, i64* %hash, i32 17
  %arrayidx.phi.trans.insert.i.14 = getelementptr inbounds i64, i64* %hash, i32 17
  %arrayidx.phi.trans.insert.i.phi.trans.insert.15 = getelementptr inbounds i64, i64* %hash, i32 18
  %arrayidx.phi.trans.insert.i.15 = getelementptr inbounds i64, i64* %hash, i32 18
  %arrayidx.phi.trans.insert.i.phi.trans.insert.16 = getelementptr inbounds i64, i64* %hash, i32 19
  %arrayidx.phi.trans.insert.i.16 = getelementptr inbounds i64, i64* %hash, i32 19
  %arrayidx.phi.trans.insert.i.phi.trans.insert.17 = getelementptr inbounds i64, i64* %hash, i32 20
  %arrayidx.phi.trans.insert.i.17 = getelementptr inbounds i64, i64* %hash, i32 20
  %arrayidx.phi.trans.insert.i.phi.trans.insert.18 = getelementptr inbounds i64, i64* %hash, i32 21
  %arrayidx.phi.trans.insert.i.18 = getelementptr inbounds i64, i64* %hash, i32 21
  %arrayidx.phi.trans.insert.i.phi.trans.insert.19 = getelementptr inbounds i64, i64* %hash, i32 22
  %arrayidx.phi.trans.insert.i.19 = getelementptr inbounds i64, i64* %hash, i32 22
  %arrayidx.phi.trans.insert.i.phi.trans.insert.20 = getelementptr inbounds i64, i64* %hash, i32 23
  %arrayidx.phi.trans.insert.i.20 = getelementptr inbounds i64, i64* %hash, i32 23
  %arrayidx.phi.trans.insert.i.phi.trans.insert.21 = getelementptr inbounds i64, i64* %hash, i32 24
  %arrayidx.phi.trans.insert.i.21 = getelementptr inbounds i64, i64* %hash, i32 24
  br label %for.body.i

for.body.i:                                       ; preds = %for.cond.cleanup6.i.for.body.i_crit_edge, %entry
  %34 = phi i64 [ %xor.4, %entry ], [ %.pre18, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %35 = phi i64 [ %xor.3, %entry ], [ %.pre17, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %36 = phi i64 [ %xor.6, %entry ], [ %.pre16, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %37 = phi i64 [ %xor.5, %entry ], [ %.pre, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %38 = phi i64 [ %.pre78.i, %entry ], [ %xor50.4.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %39 = phi i64 [ %.pre77.i, %entry ], [ %xor50.3.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %40 = phi i64 [ %xor.14, %entry ], [ %xor50.2.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %41 = phi i64 [ %xor.9, %entry ], [ %xor50.1.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %42 = phi i64 [ %.pre74.i, %entry ], [ %xor44.4.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %43 = phi i64 [ %.pre73.i, %entry ], [ %xor44.3.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %44 = phi i64 [ %xor.13, %entry ], [ %xor44.2.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %45 = phi i64 [ %xor.8, %entry ], [ %xor44.1.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %46 = phi i64 [ %.pre70.i, %entry ], [ %xor35.4.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %47 = phi i64 [ %.pre69.i, %entry ], [ %xor35.3.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %48 = phi i64 [ %xor.12, %entry ], [ %xor35.2.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %49 = phi i64 [ %xor.7, %entry ], [ %xor35.1.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %50 = phi i64 [ %.pre66.i, %entry ], [ %xor23.4.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %51 = phi i64 [ %xor.16, %entry ], [ %xor23.3.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %52 = phi i64 [ %xor.11, %entry ], [ %xor23.2.i.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %53 = phi i64 [ %.pre63.i, %entry ], [ %xor.4.i53.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %54 = phi i64 [ %xor.15, %entry ], [ %xor.3.i51.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %55 = phi i64 [ %xor.10, %entry ], [ %xor.2.i49.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %56 = phi i64 [ %.pre.i, %entry ], [ %xor23.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %indvars.iv58.i = phi i32 [ 0, %entry ], [ %indvars.iv.next59.i, %for.cond.cleanup6.i.for.body.i_crit_edge ]
  %xor.i.i = xor i64 %54, %53
  %xor.1.i.i = xor i64 %xor.i.i, %55
  %xor.2.i.i = xor i64 %xor.1.i.i, %56
  %xor.3.i.i = xor i64 %xor.2.i.i, %37
  %57 = load i64, i64* %arrayidx3.1, align 8, !tbaa !3
  %xor.1115.i.i = xor i64 %51, %50
  %xor.1.1.i.i = xor i64 %xor.1115.i.i, %52
  %xor.2.1.i.i = xor i64 %xor.1.1.i.i, %57
  %xor.3.1.i.i = xor i64 %xor.2.1.i.i, %36
  %58 = load i64, i64* %arrayidx3.2, align 8, !tbaa !3
  %xor.2118.i.i = xor i64 %47, %46
  %xor.1.2.i.i = xor i64 %xor.2118.i.i, %48
  %xor.2.2.i.i = xor i64 %xor.1.2.i.i, %49
  %xor.3.2.i.i = xor i64 %xor.2.2.i.i, %58
  %xor.3121.i.i = xor i64 %43, %42
  %xor.1.3.i.i = xor i64 %xor.3121.i.i, %44
  %xor.2.3.i.i = xor i64 %xor.1.3.i.i, %45
  %xor.3.3.i.i = xor i64 %xor.2.3.i.i, %35
  %xor.4.i.i = xor i64 %39, %38
  %xor.1.4.i.i = xor i64 %xor.4.i.i, %40
  %xor.2.4.i.i = xor i64 %xor.1.4.i.i, %41
  %xor.3.4.i.i = xor i64 %xor.2.4.i.i, %34
  %shl.i.i = shl i64 %xor.3.1.i.i, 1
  %shr.i.i = lshr i64 %xor.3.1.i.i, 63
  %xor3493.i.i = or i64 %shl.i.i, %shr.i.i
  %xor39.i.i = xor i64 %xor.3.4.i.i, %xor3493.i.i
  %shl.1.i.i = shl i64 %xor.3.2.i.i, 1
  %shr.1.i.i = lshr i64 %xor.3.2.i.i, 63
  %xor3493.1.i.i = or i64 %shl.1.i.i, %shr.1.i.i
  %xor39.1.i.i = xor i64 %xor3493.1.i.i, %xor.3.i.i
  %shl.2.i.i = shl i64 %xor.3.3.i.i, 1
  %shr.2.i.i = lshr i64 %xor.3.3.i.i, 63
  %xor3493.2.i.i = or i64 %shl.2.i.i, %shr.2.i.i
  %xor39.2.i.i = xor i64 %xor3493.2.i.i, %xor.3.1.i.i
  %shl.3.i.i = shl i64 %xor.3.4.i.i, 1
  %shr.3.i.i = lshr i64 %xor.3.4.i.i, 63
  %xor3493.3.i.i = or i64 %shl.3.i.i, %shr.3.i.i
  %xor39.3.i.i = xor i64 %xor3493.3.i.i, %xor.3.2.i.i
  %shl.4.i.i = shl i64 %xor.3.i.i, 1
  %shr.4.i.i = lshr i64 %xor.3.i.i, 63
  %xor3493.4.i.i = or i64 %shl.4.i.i, %shr.4.i.i
  %xor39.4.i.i = xor i64 %xor.3.3.i.i, %xor3493.4.i.i
  %xor65.i.i = xor i64 %xor39.i.i, %56
  store i64 %xor65.i.i, i64* %hash, align 8, !tbaa !3
  %xor65.1.i.i = xor i64 %xor39.i.i, %37
  store i64 %xor65.1.i.i, i64* %arrayidx3.5, align 8, !tbaa !3
  %xor65.2.i.i = xor i64 %xor39.i.i, %55
  store i64 %xor65.2.i.i, i64* %arrayidx3.10, align 8, !tbaa !3
  %xor65.3.i.i = xor i64 %xor39.i.i, %54
  store i64 %xor65.3.i.i, i64* %arrayidx3.15, align 8, !tbaa !3
  %xor65.4.i.i = xor i64 %xor39.i.i, %53
  store i64 %xor65.4.i.i, i64* %arrayidx12.3.i.i, align 8, !tbaa !3
  %xor65.1105.i.i = xor i64 %xor39.1.i.i, %57
  %xor65.1.1.i.i = xor i64 %xor39.1.i.i, %36
  store i64 %xor65.1.1.i.i, i64* %arrayidx3.6, align 8, !tbaa !3
  %xor65.2.1.i.i = xor i64 %xor39.1.i.i, %52
  store i64 %xor65.2.1.i.i, i64* %arrayidx3.11, align 8, !tbaa !3
  %xor65.3.1.i.i = xor i64 %xor39.1.i.i, %51
  store i64 %xor65.3.1.i.i, i64* %arrayidx3.16, align 8, !tbaa !3
  %xor65.4.1.i.i = xor i64 %xor39.1.i.i, %50
  store i64 %xor65.4.1.i.i, i64* %arrayidx12.3.1.i.i, align 8, !tbaa !3
  %xor65.2107.i.i = xor i64 %xor39.2.i.i, %58
  store i64 %xor65.2107.i.i, i64* %arrayidx3.2, align 8, !tbaa !3
  %xor65.1.2.i.i = xor i64 %xor39.2.i.i, %49
  store i64 %xor65.1.2.i.i, i64* %arrayidx3.7, align 8, !tbaa !3
  %xor65.2.2.i.i = xor i64 %xor39.2.i.i, %48
  store i64 %xor65.2.2.i.i, i64* %arrayidx3.12, align 8, !tbaa !3
  %xor65.3.2.i.i = xor i64 %xor39.2.i.i, %47
  store i64 %xor65.3.2.i.i, i64* %arrayidx12.2.2.i.i, align 8, !tbaa !3
  %xor65.4.2.i.i = xor i64 %xor39.2.i.i, %46
  store i64 %xor65.4.2.i.i, i64* %arrayidx12.3.2.i.i, align 8, !tbaa !3
  %xor65.3109.i.i = xor i64 %xor39.3.i.i, %35
  store i64 %xor65.3109.i.i, i64* %arrayidx3.3, align 8, !tbaa !3
  %xor65.1.3.i.i = xor i64 %xor39.3.i.i, %45
  store i64 %xor65.1.3.i.i, i64* %arrayidx3.8, align 8, !tbaa !3
  %xor65.2.3.i.i = xor i64 %xor39.3.i.i, %44
  store i64 %xor65.2.3.i.i, i64* %arrayidx3.13, align 8, !tbaa !3
  %xor65.3.3.i.i = xor i64 %xor39.3.i.i, %43
  store i64 %xor65.3.3.i.i, i64* %arrayidx12.2.3.i.i, align 8, !tbaa !3
  %xor65.4.3.i.i = xor i64 %xor39.3.i.i, %42
  store i64 %xor65.4.3.i.i, i64* %arrayidx12.3.3.i.i, align 8, !tbaa !3
  %xor65.4111.i.i = xor i64 %xor39.4.i.i, %34
  store i64 %xor65.4111.i.i, i64* %arrayidx3.4, align 8, !tbaa !3
  %xor65.1.4.i.i = xor i64 %xor39.4.i.i, %41
  store i64 %xor65.1.4.i.i, i64* %arrayidx3.9, align 8, !tbaa !3
  %xor65.2.4.i.i = xor i64 %xor39.4.i.i, %40
  store i64 %xor65.2.4.i.i, i64* %arrayidx3.14, align 8, !tbaa !3
  %xor65.3.4.i.i = xor i64 %xor39.4.i.i, %39
  store i64 %xor65.3.4.i.i, i64* %arrayidx12.2.4.i.i, align 8, !tbaa !3
  %xor65.4.4.i.i = xor i64 %xor39.4.i.i, %38
  store i64 %xor65.4.4.i.i, i64* %arrayidx12.3.4.i.i, align 8, !tbaa !3
  %shl.i9 = shl i64 %xor65.1105.i.i, 1
  %shr.i10 = lshr i64 %xor65.1105.i.i, 63
  %xor.i1115 = or i64 %shr.i10, %shl.i9
  store i64 %xor.i1115, i64* %arrayidx3.1, align 8, !tbaa !3
  %shl.i21 = shl i64 %xor65.2107.i.i, 62
  %shr.i22 = lshr i64 %xor65.2107.i.i, 2
  %xor.i2325 = or i64 %shr.i22, %shl.i21
  store i64 %xor.i2325, i64* %arrayidx.phi.trans.insert.i20, align 8, !tbaa !3
  %.pre79.i.pre = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert, align 8, !tbaa !3
  %shl.i = shl i64 %.pre79.i.pre, 28
  %shr.i = lshr i64 %.pre79.i.pre, 36
  %xor.i26 = or i64 %shr.i, %shl.i
  store i64 %xor.i26, i64* %arrayidx.phi.trans.insert.i, align 8, !tbaa !3
  %.pre79.i.pre.1 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.1, align 8, !tbaa !3
  %shl.i.1 = shl i64 %.pre79.i.pre.1, 27
  %shr.i.1 = lshr i64 %.pre79.i.pre.1, 37
  %xor.i.127 = or i64 %shr.i.1, %shl.i.1
  store i64 %xor.i.127, i64* %arrayidx.phi.trans.insert.i.1, align 8, !tbaa !3
  %.pre79.i.pre.2 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.2, align 8, !tbaa !3
  %shl.i.2 = shl i64 %.pre79.i.pre.2, 36
  %shr.i.2 = lshr i64 %.pre79.i.pre.2, 28
  %xor.i.228 = or i64 %shr.i.2, %shl.i.2
  store i64 %xor.i.228, i64* %arrayidx.phi.trans.insert.i.2, align 8, !tbaa !3
  %.pre79.i.pre.3 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.3, align 8, !tbaa !3
  %shl.i.3 = shl i64 %.pre79.i.pre.3, 44
  %shr.i.3 = lshr i64 %.pre79.i.pre.3, 20
  %xor.i.329 = or i64 %shr.i.3, %shl.i.3
  store i64 %xor.i.329, i64* %arrayidx.phi.trans.insert.i.3, align 8, !tbaa !3
  %.pre79.i.pre.4 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.4, align 8, !tbaa !3
  %shl.i.4 = shl i64 %.pre79.i.pre.4, 6
  %shr.i.4 = lshr i64 %.pre79.i.pre.4, 58
  %xor.i.430 = or i64 %shr.i.4, %shl.i.4
  store i64 %xor.i.430, i64* %arrayidx.phi.trans.insert.i.4, align 8, !tbaa !3
  %.pre79.i.pre.5 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.5, align 8, !tbaa !3
  %shl.i.5 = shl i64 %.pre79.i.pre.5, 55
  %shr.i.5 = lshr i64 %.pre79.i.pre.5, 9
  %xor.i.531 = or i64 %shr.i.5, %shl.i.5
  store i64 %xor.i.531, i64* %arrayidx.phi.trans.insert.i.5, align 8, !tbaa !3
  %.pre79.i.pre.6 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.6, align 8, !tbaa !3
  %shl.i.6 = shl i64 %.pre79.i.pre.6, 20
  %shr.i.6 = lshr i64 %.pre79.i.pre.6, 44
  %xor.i.632 = or i64 %shr.i.6, %shl.i.6
  store i64 %xor.i.632, i64* %arrayidx.phi.trans.insert.i.6, align 8, !tbaa !3
  %.pre79.i.pre.7 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.7, align 8, !tbaa !3
  %shl.i.7 = shl i64 %.pre79.i.pre.7, 3
  %shr.i.7 = lshr i64 %.pre79.i.pre.7, 61
  %xor.i.733 = or i64 %shr.i.7, %shl.i.7
  store i64 %xor.i.733, i64* %arrayidx.phi.trans.insert.i.7, align 8, !tbaa !3
  %.pre79.i.pre.8 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.8, align 8, !tbaa !3
  %shl.i.8 = shl i64 %.pre79.i.pre.8, 10
  %shr.i.8 = lshr i64 %.pre79.i.pre.8, 54
  %xor.i.834 = or i64 %shr.i.8, %shl.i.8
  store i64 %xor.i.834, i64* %arrayidx.phi.trans.insert.i.8, align 8, !tbaa !3
  %.pre79.i.pre.9 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.9, align 8, !tbaa !3
  %shl.i.9 = shl i64 %.pre79.i.pre.9, 43
  %shr.i.9 = lshr i64 %.pre79.i.pre.9, 21
  %xor.i.935 = or i64 %shr.i.9, %shl.i.9
  store i64 %xor.i.935, i64* %arrayidx.phi.trans.insert.i.9, align 8, !tbaa !3
  %.pre79.i.pre.10 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.10, align 8, !tbaa !3
  %shl.i.10 = shl i64 %.pre79.i.pre.10, 25
  %shr.i.10 = lshr i64 %.pre79.i.pre.10, 39
  %xor.i.1036 = or i64 %shr.i.10, %shl.i.10
  store i64 %xor.i.1036, i64* %arrayidx.phi.trans.insert.i.10, align 8, !tbaa !3
  %.pre79.i.pre.11 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.11, align 8, !tbaa !3
  %shl.i.11 = shl i64 %.pre79.i.pre.11, 39
  %shr.i.11 = lshr i64 %.pre79.i.pre.11, 25
  %xor.i.1137 = or i64 %shr.i.11, %shl.i.11
  store i64 %xor.i.1137, i64* %arrayidx.phi.trans.insert.i.11, align 8, !tbaa !3
  %.pre79.i.pre.12 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.12, align 8, !tbaa !3
  %shl.i.12 = shl i64 %.pre79.i.pre.12, 41
  %shr.i.12 = lshr i64 %.pre79.i.pre.12, 23
  %xor.i.1238 = or i64 %shr.i.12, %shl.i.12
  store i64 %xor.i.1238, i64* %arrayidx.phi.trans.insert.i.12, align 8, !tbaa !3
  %.pre79.i.pre.13 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.13, align 8, !tbaa !3
  %shl.i.13 = shl i64 %.pre79.i.pre.13, 45
  %shr.i.13 = lshr i64 %.pre79.i.pre.13, 19
  %xor.i.1339 = or i64 %shr.i.13, %shl.i.13
  store i64 %xor.i.1339, i64* %arrayidx.phi.trans.insert.i.13, align 8, !tbaa !3
  %.pre79.i.pre.14 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.14, align 8, !tbaa !3
  %shl.i.14 = shl i64 %.pre79.i.pre.14, 15
  %shr.i.14 = lshr i64 %.pre79.i.pre.14, 49
  %xor.i.1440 = or i64 %shr.i.14, %shl.i.14
  store i64 %xor.i.1440, i64* %arrayidx.phi.trans.insert.i.14, align 8, !tbaa !3
  %.pre79.i.pre.15 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.15, align 8, !tbaa !3
  %shl.i.15 = shl i64 %.pre79.i.pre.15, 21
  %shr.i.15 = lshr i64 %.pre79.i.pre.15, 43
  %xor.i.1541 = or i64 %shr.i.15, %shl.i.15
  store i64 %xor.i.1541, i64* %arrayidx.phi.trans.insert.i.15, align 8, !tbaa !3
  %.pre79.i.pre.16 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.16, align 8, !tbaa !3
  %shl.i.16 = shl i64 %.pre79.i.pre.16, 8
  %shr.i.16 = lshr i64 %.pre79.i.pre.16, 56
  %xor.i.1642 = or i64 %shr.i.16, %shl.i.16
  store i64 %xor.i.1642, i64* %arrayidx.phi.trans.insert.i.16, align 8, !tbaa !3
  %.pre79.i.pre.17 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.17, align 8, !tbaa !3
  %shl.i.17 = shl i64 %.pre79.i.pre.17, 18
  %shr.i.17 = lshr i64 %.pre79.i.pre.17, 46
  %xor.i.1743 = or i64 %shr.i.17, %shl.i.17
  store i64 %xor.i.1743, i64* %arrayidx.phi.trans.insert.i.17, align 8, !tbaa !3
  %.pre79.i.pre.18 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.18, align 8, !tbaa !3
  %shl.i.18 = shl i64 %.pre79.i.pre.18, 2
  %shr.i.18 = lshr i64 %.pre79.i.pre.18, 62
  %xor.i.1844 = or i64 %shr.i.18, %shl.i.18
  store i64 %xor.i.1844, i64* %arrayidx.phi.trans.insert.i.18, align 8, !tbaa !3
  %.pre79.i.pre.19 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.19, align 8, !tbaa !3
  %shl.i.19 = shl i64 %.pre79.i.pre.19, 61
  %shr.i.19 = lshr i64 %.pre79.i.pre.19, 3
  %xor.i.1945 = or i64 %shr.i.19, %shl.i.19
  store i64 %xor.i.1945, i64* %arrayidx.phi.trans.insert.i.19, align 8, !tbaa !3
  %.pre79.i.pre.20 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.20, align 8, !tbaa !3
  %shl.i.20 = shl i64 %.pre79.i.pre.20, 56
  %shr.i.20 = lshr i64 %.pre79.i.pre.20, 8
  %xor.i.2046 = or i64 %shr.i.20, %shl.i.20
  store i64 %xor.i.2046, i64* %arrayidx.phi.trans.insert.i.20, align 8, !tbaa !3
  %.pre79.i.pre.21 = load i64, i64* %arrayidx.phi.trans.insert.i.phi.trans.insert.21, align 8, !tbaa !3
  %shl.i.21 = shl i64 %.pre79.i.pre.21, 14
  %shr.i.21 = lshr i64 %.pre79.i.pre.21, 50
  %xor.i.2147 = or i64 %shr.i.21, %shl.i.21
  store i64 %xor.i.2147, i64* %arrayidx.phi.trans.insert.i.21, align 8, !tbaa !3
  %59 = load i64, i64* %arrayidx3.1, align 8, !tbaa !3
  %60 = load i64, i64* %arrayidx3.6, align 8, !tbaa !3
  %61 = load i64, i64* %arrayidx3.9, align 8, !tbaa !3
  %62 = load i64, i64* %arrayidx12.3.2.i.i, align 8, !tbaa !3
  %63 = load i64, i64* %arrayidx3.14, align 8, !tbaa !3
  store i64 %63, i64* %arrayidx12.3.2.i.i, align 8, !tbaa !3
  %64 = load i64, i64* %arrayidx12.3.i.i, align 8, !tbaa !3
  %65 = load i64, i64* %arrayidx3.2, align 8, !tbaa !3
  store i64 %65, i64* %arrayidx12.3.i.i, align 8, !tbaa !3
  %66 = load i64, i64* %arrayidx3.12, align 8, !tbaa !3
  %67 = load i64, i64* %arrayidx3.13, align 8, !tbaa !3
  %68 = load i64, i64* %arrayidx12.2.4.i.i, align 8, !tbaa !3
  %69 = load i64, i64* %arrayidx12.3.3.i.i, align 8, !tbaa !3
  %70 = load i64, i64* %arrayidx3.15, align 8, !tbaa !3
  store i64 %70, i64* %arrayidx12.3.3.i.i, align 8, !tbaa !3
  %71 = load i64, i64* %arrayidx3.4, align 8, !tbaa !3
  %72 = load i64, i64* %arrayidx12.3.4.i.i, align 8, !tbaa !3
  %73 = load i64, i64* %arrayidx12.3.1.i.i, align 8, !tbaa !3
  store i64 %73, i64* %arrayidx12.3.4.i.i, align 8, !tbaa !3
  %74 = load i64, i64* %arrayidx3.8, align 8, !tbaa !3
  %75 = load i64, i64* %arrayidx3.16, align 8, !tbaa !3
  %76 = load i64, i64* %arrayidx3.5, align 8, !tbaa !3
  %77 = load i64, i64* %arrayidx3.3, align 8, !tbaa !3
  %78 = load i64, i64* %arrayidx12.2.3.i.i, align 8, !tbaa !3
  %79 = load i64, i64* %arrayidx12.2.2.i.i, align 8, !tbaa !3
  %80 = load i64, i64* %arrayidx3.11, align 8, !tbaa !3
  %81 = load i64, i64* %arrayidx3.7, align 8, !tbaa !3
  %82 = load i64, i64* %arrayidx3.10, align 8, !tbaa !3
  %83 = load i64, i64* %hash, align 8, !tbaa !3
  %neg.i.i = xor i64 %60, -1
  %and.i44.i = and i64 %66, %neg.i.i
  %xor.i45.i = xor i64 %83, %and.i44.i
  store i64 %xor.i45.i, i64* %hash, align 8, !tbaa !3
  %neg15.i.i = xor i64 %66, -1
  %and19.i.i = and i64 %78, %neg15.i.i
  %xor23.i.i = xor i64 %and19.i.i, %60
  store i64 %xor23.i.i, i64* %arrayidx3.1, align 8, !tbaa !3
  %neg27.i.i = xor i64 %78, -1
  %and31.i.i = and i64 %72, %neg27.i.i
  %xor35.i.i = xor i64 %and31.i.i, %66
  store i64 %xor35.i.i, i64* %arrayidx3.2, align 8, !tbaa !3
  %neg39.i.i = xor i64 %72, -1
  %and40.i.i = and i64 %83, %neg39.i.i
  %xor44.i.i = xor i64 %and40.i.i, %78
  store i64 %xor44.i.i, i64* %arrayidx3.3, align 8, !tbaa !3
  %neg45.i.i = xor i64 %83, -1
  %and46.i.i = and i64 %60, %neg45.i.i
  %xor50.i.i = xor i64 %and46.i.i, %72
  store i64 %xor50.i.i, i64* %arrayidx3.4, align 8, !tbaa !3
  %neg.1.i.i = xor i64 %61, -1
  %and.1.i.i = and i64 %82, %neg.1.i.i
  %xor.1.i47.i = xor i64 %and.1.i.i, %77
  store i64 %xor.1.i47.i, i64* %arrayidx3.5, align 8, !tbaa !3
  %neg15.1.i.i = xor i64 %82, -1
  %and19.1.i.i = and i64 %75, %neg15.1.i.i
  %xor23.1.i.i = xor i64 %and19.1.i.i, %61
  store i64 %xor23.1.i.i, i64* %arrayidx3.6, align 8, !tbaa !3
  %neg27.1.i.i = xor i64 %75, -1
  %and31.1.i.i = and i64 %62, %neg27.1.i.i
  %xor35.1.i.i = xor i64 %82, %and31.1.i.i
  store i64 %xor35.1.i.i, i64* %arrayidx3.7, align 8, !tbaa !3
  %neg39.1.i.i = xor i64 %62, -1
  %and40.1.i.i = and i64 %77, %neg39.1.i.i
  %xor44.1.i.i = xor i64 %and40.1.i.i, %75
  store i64 %xor44.1.i.i, i64* %arrayidx3.8, align 8, !tbaa !3
  %neg45.1.i.i = xor i64 %77, -1
  %and46.1.i.i = and i64 %61, %neg45.1.i.i
  %xor50.1.i.i = xor i64 %and46.1.i.i, %62
  store i64 %xor50.1.i.i, i64* %arrayidx3.9, align 8, !tbaa !3
  %neg.2.i.i = xor i64 %81, -1
  %and.2.i.i = and i64 %67, %neg.2.i.i
  %xor.2.i49.i = xor i64 %and.2.i.i, %59
  store i64 %xor.2.i49.i, i64* %arrayidx3.10, align 8, !tbaa !3
  %neg15.2.i.i = xor i64 %67, -1
  %and19.2.i.i = and i64 %68, %neg15.2.i.i
  %xor23.2.i.i = xor i64 %81, %and19.2.i.i
  store i64 %xor23.2.i.i, i64* %arrayidx3.11, align 8, !tbaa !3
  %neg27.2.i.i = xor i64 %68, -1
  %and31.2.i.i = and i64 %64, %neg27.2.i.i
  %xor35.2.i.i = xor i64 %and31.2.i.i, %67
  store i64 %xor35.2.i.i, i64* %arrayidx3.12, align 8, !tbaa !3
  %neg39.2.i.i = xor i64 %64, -1
  %and40.2.i.i = and i64 %59, %neg39.2.i.i
  %xor44.2.i.i = xor i64 %68, %and40.2.i.i
  store i64 %xor44.2.i.i, i64* %arrayidx3.13, align 8, !tbaa !3
  %neg45.2.i.i = xor i64 %59, -1
  %and46.2.i.i = and i64 %81, %neg45.2.i.i
  %xor50.2.i.i = xor i64 %and46.2.i.i, %64
  store i64 %xor50.2.i.i, i64* %arrayidx3.14, align 8, !tbaa !3
  %neg.3.i.i = xor i64 %76, -1
  %and.3.i.i = and i64 %80, %neg.3.i.i
  %xor.3.i51.i = xor i64 %and.3.i.i, %71
  store i64 %xor.3.i51.i, i64* %arrayidx3.15, align 8, !tbaa !3
  %neg15.3.i.i = xor i64 %80, -1
  %and19.3.i.i = and i64 %79, %neg15.3.i.i
  %xor23.3.i.i = xor i64 %and19.3.i.i, %76
  store i64 %xor23.3.i.i, i64* %arrayidx3.16, align 8, !tbaa !3
  %neg27.3.i.i = xor i64 %79, -1
  %and31.3.i.i = and i64 %69, %neg27.3.i.i
  %xor35.3.i.i = xor i64 %80, %and31.3.i.i
  store i64 %xor35.3.i.i, i64* %arrayidx12.2.2.i.i, align 8, !tbaa !3
  %neg39.3.i.i = xor i64 %69, -1
  %and40.3.i.i = and i64 %71, %neg39.3.i.i
  %xor44.3.i.i = xor i64 %79, %and40.3.i.i
  store i64 %xor44.3.i.i, i64* %arrayidx12.2.3.i.i, align 8, !tbaa !3
  %neg45.3.i.i = xor i64 %71, -1
  %and46.3.i.i = and i64 %76, %neg45.3.i.i
  %xor50.3.i.i = xor i64 %and46.3.i.i, %69
  store i64 %xor50.3.i.i, i64* %arrayidx12.2.4.i.i, align 8, !tbaa !3
  %neg.4.i.i = xor i64 %74, -1
  %and.4.i.i = and i64 %63, %neg.4.i.i
  %xor.4.i53.i = xor i64 %and.4.i.i, %65
  store i64 %xor.4.i53.i, i64* %arrayidx12.3.i.i, align 8, !tbaa !3
  %neg15.4.i.i = xor i64 %63, -1
  %and19.4.i.i = and i64 %70, %neg15.4.i.i
  %xor23.4.i.i = xor i64 %74, %and19.4.i.i
  store i64 %xor23.4.i.i, i64* %arrayidx12.3.1.i.i, align 8, !tbaa !3
  %neg27.4.i.i = xor i64 %70, -1
  %and31.4.i.i = and i64 %73, %neg27.4.i.i
  %xor35.4.i.i = xor i64 %and31.4.i.i, %63
  store i64 %xor35.4.i.i, i64* %arrayidx12.3.2.i.i, align 8, !tbaa !3
  %neg39.4.i.i = xor i64 %73, -1
  %and40.4.i.i = and i64 %65, %neg39.4.i.i
  %xor44.4.i.i = xor i64 %and40.4.i.i, %70
  store i64 %xor44.4.i.i, i64* %arrayidx12.3.3.i.i, align 8, !tbaa !3
  %neg45.4.i.i = xor i64 %65, -1
  %and46.4.i.i = and i64 %74, %neg45.4.i.i
  %xor50.4.i.i = xor i64 %and46.4.i.i, %73
  store i64 %xor50.4.i.i, i64* %arrayidx12.3.4.i.i, align 8, !tbaa !3
  %arrayidx.i.i.i = getelementptr inbounds [72 x i8], [72 x i8]* @constants, i32 0, i32 %indvars.iv58.i
  %84 = load i8, i8* %arrayidx.i.i.i, align 1, !tbaa !7
  %conv.i.i = zext i8 %84 to i32
  %and.i.i = and i32 %conv.i.i, 64
  %85 = zext i32 %and.i.i to i64
  %86 = shl nuw i64 %85, 57
  %and2.i.i = shl i32 %conv.i.i, 26
  %87 = and i32 %and2.i.i, -2147483648
  %and8.i.i = shl nuw nsw i32 %conv.i.i, 11
  %88 = and i32 %and8.i.i, 32768
  %89 = zext i32 %88 to i64
  %and14.i.i = shl nuw nsw i32 %conv.i.i, 4
  %90 = and i32 %and14.i.i, 128
  %91 = zext i32 %90 to i64
  %and20.i.i = shl nuw nsw i32 %conv.i.i, 1
  %92 = and i32 %and20.i.i, 8
  %93 = zext i32 %92 to i64
  %94 = and i32 %conv.i.i, 3
  %95 = or i32 %87, %94
  %96 = zext i32 %95 to i64
  %97 = or i64 %86, %89
  %98 = or i64 %97, %91
  %99 = or i64 %98, %93
  %100 = or i64 %99, %96
  %xor23.i = xor i64 %100, %xor.i45.i
  store i64 %xor23.i, i64* %hash, align 8, !tbaa !3
  %indvars.iv.next59.i = add nuw nsw i32 %indvars.iv58.i, 1
  %exitcond60.not.i = icmp eq i32 %indvars.iv.next59.i, 24
  br i1 %exitcond60.not.i, label %sha3_permutation.exit, label %for.cond.cleanup6.i.for.body.i_crit_edge

for.cond.cleanup6.i.for.body.i_crit_edge:         ; preds = %for.body.i
  %.pre = load i64, i64* %arrayidx3.5, align 8, !tbaa !3
  %.pre16 = load i64, i64* %arrayidx3.6, align 8, !tbaa !3
  %.pre17 = load i64, i64* %arrayidx3.3, align 8, !tbaa !3
  %.pre18 = load i64, i64* %arrayidx3.4, align 8, !tbaa !3
  br label %for.body.i

sha3_permutation.exit:                            ; preds = %for.body.i
  ret void
}

; Function Attrs: nounwind
define internal void @keccak_final(%struct.SHA3_CTX* %ctx, i8* %result) local_unnamed_addr #2 {
entry:
  %arraydecay = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 1, i32 0
  %0 = bitcast i64* %arraydecay to i8*
  %rest = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 2
  %1 = load i16, i16* %rest, align 8, !tbaa !23
  %conv = zext i16 %1 to i32
  %add.ptr = getelementptr inbounds i8, i8* %0, i32 %conv
  %sub = sub nsw i32 136, %conv
  tail call void @__memset(i8* nonnull %add.ptr, i8 zeroext 0, i32 %sub) #14
  %2 = load i16, i16* %rest, align 8, !tbaa !23
  %idxprom = zext i16 %2 to i32
  %arrayidx = getelementptr inbounds i8, i8* %0, i32 %idxprom
  %3 = load i8, i8* %arrayidx, align 1, !tbaa !7
  %4 = or i8 %3, 1
  store i8 %4, i8* %arrayidx, align 1, !tbaa !7
  %arrayidx10 = getelementptr inbounds i8, i8* %0, i32 135
  %5 = load i8, i8* %arrayidx10, align 1, !tbaa !7
  %or12 = or i8 %5, -128
  store i8 %or12, i8* %arrayidx10, align 1, !tbaa !7
  %arraydecay14 = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 0, i32 0
  tail call fastcc void @sha3_process_block(i64* %arraydecay14, i64* nonnull %arraydecay) #15
  %tobool.not = icmp eq i8* %result, null
  br i1 %tobool.not, label %if.end, label %if.then

if.then:                                          ; preds = %entry
  %6 = bitcast %struct.SHA3_CTX* %ctx to i8*
  tail call void @__memcpy(i8* nonnull %result, i8* %6, i32 32) #14
  br label %if.end

if.end:                                           ; preds = %entry, %if.then
  ret void
}

; Function Attrs: nounwind
define internal void @keccak256(i8* %data, i32 %size, i8* %out) local_unnamed_addr #2 {
entry:
  %ctx = alloca %struct.SHA3_CTX, align 8
  %0 = bitcast %struct.SHA3_CTX* %ctx to i8*
  call void @llvm.lifetime.start.p0i8(i64 400, i8* nonnull %0) #16
  call void @__memset(i8* nonnull %0, i8 zeroext 0, i32 400) #14
  %conv = trunc i32 %size to i16
  call void @keccak_update(%struct.SHA3_CTX* nonnull %ctx, i8* %data, i16 zeroext %conv) #15
  %arraydecay.i = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 1, i32 0
  %1 = bitcast i64* %arraydecay.i to i8*
  %rest.i = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 2
  %2 = load i16, i16* %rest.i, align 8, !tbaa !23
  %conv.i = zext i16 %2 to i32
  %add.ptr.i = getelementptr inbounds i8, i8* %1, i32 %conv.i
  %sub.i = sub nsw i32 136, %conv.i
  call void @__memset(i8* nonnull %add.ptr.i, i8 zeroext 0, i32 %sub.i) #14
  %3 = load i16, i16* %rest.i, align 8, !tbaa !23
  %idxprom.i = zext i16 %3 to i32
  %arrayidx.i = getelementptr inbounds i8, i8* %1, i32 %idxprom.i
  %4 = load i8, i8* %arrayidx.i, align 1, !tbaa !7
  %5 = or i8 %4, 1
  store i8 %5, i8* %arrayidx.i, align 1, !tbaa !7
  %arrayidx10.i = getelementptr inbounds i8, i8* %1, i32 135
  %6 = load i8, i8* %arrayidx10.i, align 1, !tbaa !7
  %or12.i = or i8 %6, -128
  store i8 %or12.i, i8* %arrayidx10.i, align 1, !tbaa !7
  %arraydecay14.i = getelementptr inbounds %struct.SHA3_CTX, %struct.SHA3_CTX* %ctx, i32 0, i32 0, i32 0
  call fastcc void @sha3_process_block(i64* nonnull %arraydecay14.i, i64* nonnull %arraydecay.i) #14
  %tobool.not.i = icmp eq i8* %out, null
  br i1 %tobool.not.i, label %keccak_final.exit, label %if.then.i

if.then.i:                                        ; preds = %entry
  call void @__memcpy(i8* nonnull %out, i8* nonnull %0, i32 32) #14
  br label %keccak_final.exit

keccak_final.exit:                                ; preds = %entry, %if.then.i
  call void @llvm.lifetime.end.p0i8(i64 400, i8* nonnull %0) #16
  ret void
}

declare internal void @save_storage(i8*, i8*)

declare internal void @load_storage(i8*, i8*)

declare internal void @save_storage_string(i8*, i8*, i32)

declare internal void @load_storage_string(i8*, i8*)

declare internal i32 @get_storage_string_size(i8*)

declare internal i32 @get_call_size()

declare internal i32 @get_return_size()

declare internal void @copy_call_value(i32, i32, i8*)

declare internal void @copy_return_value(i8*, i32, i32)

declare internal i32 @invoke_contract(i8*, i32, i8*, i8*, i8*)

declare internal i32 @invoke_static_contract(i8*, i32, i8*, i8*, i8*)

declare internal i32 @invoke_delegate_contract(i8*, i32, i8*, i8*, i8*)

declare internal void @get_msgvalue(i8*)

declare internal void @get_address(i8*)

declare internal void @get_sender(i8*)

declare internal void @get_external_balance(i8*, i8*)

declare internal void @get_gas_left(i8*)

declare internal void @get_tx_gas_price(i8*)

declare internal void @get_tx_origin(i8*)

declare internal void @get_block_number(i8*)

declare internal void @get_block_gas_limit(i8*)

declare internal void @get_block_difficulty(i8*)

declare internal void @get_block_coinbase_address(i8*)

declare internal void @get_block_timestamp(i8*)

declare internal void @get_chain_id(i8*)

declare internal i32 @create(i8*, i8*, i32, i8*)

declare internal void @write_log(i8*, i32)

declare internal void @set_return(i8*, i32)

declare internal void @crypto_keccak256(i8*, i32, i8*)

declare internal void @crypto_ripemd160(i8*, i32, i8*)

declare internal void @crypto_sha256(i8*, i32, i8*)

declare internal void @crypto_recover(i8*, i8, i8*, i8*, i8*)

; Function Attrs: noreturn
declare internal void @system_halt(i32) #13

define internal i32 @"FullMath::FullMath::function::mulDivRoundingUp__uint256_uint256_uint256"(i256 %0, i256 %1, i256 %2, i256* %3) {
entry:
  %quotient = alloca i512, align 8
  %remainder = alloca i512, align 8
  %divisor = alloca i512, align 8
  %dividend = alloca i512, align 8
  %x_times_y_m = alloca i512, align 8
  %x_y = alloca i512, align 8
  %x_m = alloca i512, align 8
  %stack = alloca i256, align 8
  %uint2564 = alloca i256, align 8
  %uint2562 = alloca i256, align 8
  %uint256 = alloca i256, align 8
  %bytes4 = alloca i32, align 4
  %4 = call i8* @__malloc(i32 add (i32 ptrtoint (%struct.vector* getelementptr (%struct.vector, %struct.vector* null, i32 1) to i32), i32 100))
  %string = bitcast i8* %4 to %struct.vector*
  %data_len = getelementptr %struct.vector, %struct.vector* %string, i32 0, i32 0
  store i32 100, i32* %data_len, align 4
  %data_size = getelementptr %struct.vector, %struct.vector* %string, i32 0, i32 1
  store i32 100, i32* %data_size, align 4
  %data = getelementptr %struct.vector, %struct.vector* %string, i32 0, i32 2
  %5 = bitcast [0 x i8]* %data to i8*
  store i32 -1432745710, i32* %bytes4, align 4
  %store = bitcast i32* %bytes4 to i8*
  call void @__leNtobeN(i8* %store, i8* %5, i32 4)
  %6 = getelementptr i8, i8* %5, i32 4
  call void @__bzero8(i8* %6, i32 12)
  %7 = getelementptr i8, i8* %6, i32 96
  store i256 %0, i256* %uint256, align 8
  %store1 = bitcast i256* %uint256 to i8*
  call void @__leNtobe32(i8* %store1, i8* %6, i32 32)
  %8 = getelementptr i8, i8* %6, i32 32
  store i256 %1, i256* %uint2562, align 8
  %store3 = bitcast i256* %uint2562 to i8*
  call void @__leNtobe32(i8* %store3, i8* %8, i32 32)
  %9 = getelementptr i8, i8* %8, i32 32
  store i256 %2, i256* %uint2564, align 8
  %store5 = bitcast i256* %uint2564 to i8*
  call void @__leNtobe32(i8* %store5, i8* %9, i32 32)
  %10 = getelementptr i8, i8* %9, i32 32
  %self_address = alloca i160, align 8
  %11 = bitcast i160* %self_address to i8*
  call void @get_address(i8* %11)
  %self_address6 = load i160, i160* %self_address, align 8
  %address = alloca i8, i32 20, align 1
  %address7 = bitcast i8* %address to i160*
  store i160 %self_address6, i160* %address7, align 8
  %data8 = getelementptr %struct.vector, %struct.vector* %string, i32 0, i32 2
  %data9 = bitcast [0 x i8]* %data8 to i8*
  %vector_len = getelementptr %struct.vector, %struct.vector* %string, i32 0, i32 0
  %vector_is_null = icmp eq %struct.vector* %string, null
  %vector_len10 = load i32, i32* %vector_len, align 4
  %length = select i1 %vector_is_null, i32 0, i32 %vector_len10
  %be_address = alloca i160, align 8
  %12 = bitcast i160* %be_address to i8*
  call void @__leNtobeN(i8* %address, i8* %12, i32 20)
  %balance = alloca i256, align 8
  store i256 0, i256* %balance, align 8
  %gas = alloca i64, align 8
  store i64 0, i64* %gas, align 8
  %address11 = bitcast i160* %be_address to i8*
  %value_transfer = bitcast i256* %balance to i8*
  %gas_transfer = bitcast i64* %gas to i8*
  %13 = call i32 @invoke_contract(i8* %address11, i32 %length, i8* %data9, i8* %value_transfer, i8* %gas_transfer)
  %success = icmp eq i32 %13, 0
  br i1 %success, label %success12, label %bail

success12:                                        ; preds = %entry
  %returndatasize = call i32 @get_return_size()
  %size = add i32 %returndatasize, ptrtoint (%struct.vector* getelementptr (%struct.vector, %struct.vector* null, i32 1) to i32)
  %14 = call i8* @__malloc(i32 %size)
  %string13 = bitcast i8* %14 to %struct.vector*
  %data_len14 = getelementptr %struct.vector, %struct.vector* %string13, i32 0, i32 0
  store i32 %returndatasize, i32* %data_len14, align 4
  %data_size15 = getelementptr %struct.vector, %struct.vector* %string13, i32 0, i32 1
  store i32 %returndatasize, i32* %data_size15, align 4
  %data16 = getelementptr %struct.vector, %struct.vector* %string13, i32 0, i32 2
  %15 = bitcast [0 x i8]* %data16 to i8*
  call void @copy_return_value(i8* %15, i32 0, i32 %returndatasize)
  %data17 = getelementptr %struct.vector, %struct.vector* %string13, i32 0, i32 2
  %data18 = bitcast [0 x i8]* %data17 to i8*
  %vector_len19 = getelementptr %struct.vector, %struct.vector* %string13, i32 0, i32 0
  %vector_is_null20 = icmp eq %struct.vector* %string13, null
  %vector_len21 = load i32, i32* %vector_len19, align 4
  %length22 = select i1 %vector_is_null20, i32 0, i32 %vector_len21
  %data_length = zext i32 %length22 to i64
  %16 = icmp ule i64 32, %data_length
  br i1 %16, label %success23, label %bail24

bail:                                             ; preds = %entry
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success23:                                        ; preds = %success12
  %17 = getelementptr i8, i8* %data18, i64 0
  %18 = bitcast i256* %stack to i8*
  call void @__be32toleN(i8* %17, i8* %18, i32 ptrtoint (i256* getelementptr (i256, i256* null, i32 1) to i32))
  %abi_int256 = load i256, i256* %stack, align 8
  %wide_x = zext i256 %0 to i512
  store i512 %wide_x, i512* %x_m, align 8
  %wide_y = zext i256 %1 to i512
  store i512 %wide_y, i512* %x_y, align 8
  %left = bitcast i512* %x_m to i32*
  %right = bitcast i512* %x_y to i32*
  %output = bitcast i512* %x_times_y_m to i32*
  call void @__mul32(i32* %left, i32* %right, i32* %output, i32 16)
  %x_t_y = load i512, i512* %x_times_y_m, align 8
  %wide_k = zext i256 %2 to i512
  store i512 %x_t_y, i512* %dividend, align 8
  store i512 %wide_k, i512* %divisor, align 8
  %quotient25 = call i32 @udivmod512(i512* %dividend, i512* %divisor, i512* %remainder, i512* %quotient)
  %success26 = icmp eq i32 %quotient25, 0
  br i1 %success26, label %success27, label %bail28

bail24:                                           ; preds = %success12
  ret i32 2

success27:                                        ; preds = %success23
  %quotient29 = load i512, i512* %quotient, align 8
  %quotient30 = trunc i512 %quotient29 to i256
  %19 = icmp ugt i256 %quotient30, 0
  br i1 %19, label %then, label %endif

bail28:                                           ; preds = %success23
  ret i32 %quotient25

then:                                             ; preds = %success27
  %20 = icmp ult i256 %abi_int256, -1
  br i1 %20, label %noassert, label %doassert

endif:                                            ; preds = %noassert, %success27
  %result = phi i256 [ %abi_int256, %success27 ], [ %21, %noassert ]
  store i256 %result, i256* %3, align 8
  ret i32 0

noassert:                                         ; preds = %then
  %21 = add i256 %abi_int256, 1
  br label %endif

doassert:                                         ; preds = %then
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable
}

define internal i32 @"FullMath::FullMath::function::mulDiv__uint256_uint256_uint256"(i256 %0, i256 %1, i256 %2, i256* %3) {
entry:
  %4 = alloca i256, align 8
  %5 = alloca i256, align 8
  %6 = alloca i256, align 8
  %7 = alloca i256, align 8
  %8 = alloca i256, align 8
  %9 = alloca i256, align 8
  %10 = alloca i256, align 8
  %11 = alloca i256, align 8
  %12 = alloca i256, align 8
  %13 = alloca i256, align 8
  %14 = alloca i256, align 8
  %15 = alloca i256, align 8
  %quotient183 = alloca i256, align 8
  %remainder182 = alloca i256, align 8
  %divisor181 = alloca i256, align 8
  %dividend180 = alloca i256, align 8
  %quotient174 = alloca i256, align 8
  %remainder173 = alloca i256, align 8
  %divisor172 = alloca i256, align 8
  %dividend171 = alloca i256, align 8
  %16 = alloca i256, align 8
  %17 = alloca i256, align 8
  %18 = alloca i256, align 8
  %quotient161 = alloca i256, align 8
  %remainder160 = alloca i256, align 8
  %divisor159 = alloca i256, align 8
  %dividend158 = alloca i256, align 8
  %19 = alloca i256, align 8
  %20 = alloca i256, align 8
  %21 = alloca i256, align 8
  %22 = alloca i256, align 8
  %23 = alloca i256, align 8
  %24 = alloca i256, align 8
  %25 = alloca i256, align 8
  %26 = alloca i256, align 8
  %27 = alloca i256, align 8
  %quotient140 = alloca i256, align 8
  %remainder139 = alloca i256, align 8
  %divisor138 = alloca i256, align 8
  %dividend137 = alloca i256, align 8
  %quotient131 = alloca i256, align 8
  %remainder130 = alloca i256, align 8
  %divisor129 = alloca i256, align 8
  %dividend128 = alloca i256, align 8
  %28 = alloca i256, align 8
  %29 = alloca i256, align 8
  %30 = alloca i256, align 8
  %quotient118 = alloca i256, align 8
  %remainder117 = alloca i256, align 8
  %divisor116 = alloca i256, align 8
  %dividend115 = alloca i256, align 8
  %31 = alloca i256, align 8
  %32 = alloca i256, align 8
  %33 = alloca i256, align 8
  %quotient105 = alloca i256, align 8
  %remainder104 = alloca i256, align 8
  %divisor103 = alloca i256, align 8
  %dividend102 = alloca i256, align 8
  %34 = alloca i256, align 8
  %35 = alloca i256, align 8
  %36 = alloca i256, align 8
  %quotient92 = alloca i256, align 8
  %remainder91 = alloca i256, align 8
  %divisor90 = alloca i256, align 8
  %dividend89 = alloca i256, align 8
  %quotient83 = alloca i256, align 8
  %remainder82 = alloca i256, align 8
  %divisor81 = alloca i256, align 8
  %dividend80 = alloca i256, align 8
  %quotient74 = alloca i256, align 8
  %remainder73 = alloca i256, align 8
  %divisor72 = alloca i256, align 8
  %dividend71 = alloca i256, align 8
  %quotient65 = alloca i256, align 8
  %remainder64 = alloca i256, align 8
  %divisor63 = alloca i256, align 8
  %dividend62 = alloca i256, align 8
  %37 = alloca i256, align 8
  %38 = alloca i256, align 8
  %39 = alloca i256, align 8
  %40 = alloca i256, align 8
  %41 = alloca i256, align 8
  %42 = alloca i256, align 8
  %quotient44 = alloca i512, align 8
  %remainder43 = alloca i512, align 8
  %divisor42 = alloca i512, align 8
  %dividend41 = alloca i512, align 8
  %x_times_y_m34 = alloca i512, align 8
  %x_y33 = alloca i512, align 8
  %x_m32 = alloca i512, align 8
  %quotient27 = alloca i256, align 8
  %remainder26 = alloca i256, align 8
  %divisor25 = alloca i256, align 8
  %dividend24 = alloca i256, align 8
  %43 = alloca i256, align 8
  %44 = alloca i256, align 8
  %45 = alloca i256, align 8
  %46 = alloca i256, align 8
  %47 = alloca i256, align 8
  %48 = alloca i256, align 8
  %49 = alloca i256, align 8
  %50 = alloca i256, align 8
  %51 = alloca i256, align 8
  %52 = alloca i256, align 8
  %53 = alloca i256, align 8
  %54 = alloca i256, align 8
  %quotient = alloca i512, align 8
  %remainder = alloca i512, align 8
  %divisor = alloca i512, align 8
  %dividend = alloca i512, align 8
  %x_times_y_m = alloca i512, align 8
  %x_y = alloca i512, align 8
  %x_m = alloca i512, align 8
  %wide_x = zext i256 %0 to i512
  store i512 %wide_x, i512* %x_m, align 8
  %wide_y = zext i256 %1 to i512
  store i512 %wide_y, i512* %x_y, align 8
  %left = bitcast i512* %x_m to i32*
  %right = bitcast i512* %x_y to i32*
  %output = bitcast i512* %x_times_y_m to i32*
  call void @__mul32(i32* %left, i32* %right, i32* %output, i32 16)
  %x_t_y = load i512, i512* %x_times_y_m, align 8
  store i512 %x_t_y, i512* %dividend, align 8
  store i512 115792089237316195423570985008687907853269984665640564039457584007913129639935, i512* %divisor, align 8
  %quotient1 = call i32 @udivmod512(i512* %dividend, i512* %divisor, i512* %remainder, i512* %quotient)
  %success = icmp eq i32 %quotient1, 0
  br i1 %success, label %success2, label %bail

success2:                                         ; preds = %entry
  %quotient3 = load i512, i512* %quotient, align 8
  %quotient4 = trunc i512 %quotient3 to i256
  store i256 %0, i256* %54, align 8
  store i256 %1, i256* %53, align 8
  %left5 = bitcast i256* %54 to i32*
  %right6 = bitcast i256* %53 to i32*
  %output7 = bitcast i256* %52 to i32*
  call void @__mul32(i32* %left5, i32* %right6, i32* %output7, i32 8)
  %mul = load i256, i256* %52, align 8
  store i256 %0, i256* %51, align 8
  store i256 %1, i256* %50, align 8
  %left8 = bitcast i256* %51 to i32*
  %right9 = bitcast i256* %50 to i32*
  %output10 = bitcast i256* %49 to i32*
  call void @__mul32(i32* %left8, i32* %right9, i32* %output10, i32 8)
  %mul11 = load i256, i256* %49, align 8
  %55 = sub i256 %quotient4, %mul11
  store i256 %0, i256* %48, align 8
  store i256 %1, i256* %47, align 8
  %left12 = bitcast i256* %48 to i32*
  %right13 = bitcast i256* %47 to i32*
  %output14 = bitcast i256* %46 to i32*
  call void @__mul32(i32* %left12, i32* %right13, i32* %output14, i32 8)
  %mul15 = load i256, i256* %46, align 8
  %56 = icmp ult i256 %quotient4, %mul15
  br i1 %56, label %then, label %endif

bail:                                             ; preds = %entry
  ret i32 %quotient1

then:                                             ; preds = %success2
  %57 = sub i256 %55, 1
  br label %endif

endif:                                            ; preds = %then, %success2
  %prod1 = phi i256 [ %55, %success2 ], [ %57, %then ]
  %58 = icmp eq i256 %prod1, 0
  br i1 %58, label %then16, label %endif17

then16:                                           ; preds = %endif
  %59 = icmp ugt i256 %2, 0
  br i1 %59, label %noassert, label %doassert

endif17:                                          ; preds = %success29, %endif
  %result = phi i256 [ 0, %endif ], [ %quotient31, %success29 ]
  %60 = icmp ugt i256 %2, %prod1
  br i1 %60, label %noassert18, label %doassert19

noassert:                                         ; preds = %then16
  store i256 %0, i256* %45, align 8
  store i256 %1, i256* %44, align 8
  %left20 = bitcast i256* %45 to i32*
  %right21 = bitcast i256* %44 to i32*
  %output22 = bitcast i256* %43 to i32*
  call void @__mul32(i32* %left20, i32* %right21, i32* %output22, i32 8)
  %mul23 = load i256, i256* %43, align 8
  store i256 %mul23, i256* %dividend24, align 8
  store i256 %2, i256* %divisor25, align 8
  %udiv = call i32 @udivmod256(i256* %dividend24, i256* %divisor25, i256* %remainder26, i256* %quotient27)
  %success28 = icmp eq i32 %udiv, 0
  br i1 %success28, label %success29, label %bail30

doassert:                                         ; preds = %then16
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

noassert18:                                       ; preds = %endif17
  %wide_x35 = zext i256 %0 to i512
  store i512 %wide_x35, i512* %x_m32, align 8
  %wide_y36 = zext i256 %1 to i512
  store i512 %wide_y36, i512* %x_y33, align 8
  %left37 = bitcast i512* %x_m32 to i32*
  %right38 = bitcast i512* %x_y33 to i32*
  %output39 = bitcast i512* %x_times_y_m34 to i32*
  call void @__mul32(i32* %left37, i32* %right38, i32* %output39, i32 16)
  %x_t_y40 = load i512, i512* %x_times_y_m34, align 8
  %wide_k = zext i256 %2 to i512
  store i512 %x_t_y40, i512* %dividend41, align 8
  store i512 %wide_k, i512* %divisor42, align 8
  %quotient45 = call i32 @udivmod512(i512* %dividend41, i512* %divisor42, i512* %remainder43, i512* %quotient44)
  %success46 = icmp eq i32 %quotient45, 0
  br i1 %success46, label %success47, label %bail48

doassert19:                                       ; preds = %endif17
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success29:                                        ; preds = %noassert
  %quotient31 = load i256, i256* %quotient27, align 8
  br label %endif17

bail30:                                           ; preds = %noassert
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success47:                                        ; preds = %noassert18
  %quotient49 = load i512, i512* %quotient44, align 8
  %quotient50 = trunc i512 %quotient49 to i256
  store i256 %0, i256* %42, align 8
  store i256 %1, i256* %41, align 8
  %left51 = bitcast i256* %42 to i32*
  %right52 = bitcast i256* %41 to i32*
  %output53 = bitcast i256* %40 to i32*
  call void @__mul32(i32* %left51, i32* %right52, i32* %output53, i32 8)
  %mul54 = load i256, i256* %40, align 8
  %61 = icmp ugt i256 %quotient50, %mul54
  br i1 %61, label %then55, label %endif56

bail48:                                           ; preds = %noassert18
  ret i32 %quotient45

then55:                                           ; preds = %success47
  %62 = sub i256 %prod1, 1
  br label %endif56

endif56:                                          ; preds = %then55, %success47
  %prod157 = phi i256 [ %prod1, %success47 ], [ %62, %then55 ]
  store i256 %0, i256* %39, align 8
  store i256 %1, i256* %38, align 8
  %left58 = bitcast i256* %39 to i32*
  %right59 = bitcast i256* %38 to i32*
  %output60 = bitcast i256* %37 to i32*
  call void @__mul32(i32* %left58, i32* %right59, i32* %output60, i32 8)
  %mul61 = load i256, i256* %37, align 8
  %63 = sub i256 %mul61, %quotient50
  %64 = sub i256 0, %2
  %65 = and i256 %64, %2
  %66 = sub i256 0, %2
  %67 = and i256 %66, %2
  store i256 %2, i256* %dividend62, align 8
  store i256 %67, i256* %divisor63, align 8
  %udiv66 = call i32 @udivmod256(i256* %dividend62, i256* %divisor63, i256* %remainder64, i256* %quotient65)
  %success67 = icmp eq i32 %udiv66, 0
  br i1 %success67, label %success68, label %bail69

success68:                                        ; preds = %endif56
  %quotient70 = load i256, i256* %quotient65, align 8
  %68 = sub i256 0, %2
  %69 = and i256 %68, %2
  store i256 %63, i256* %dividend71, align 8
  store i256 %69, i256* %divisor72, align 8
  %udiv75 = call i32 @udivmod256(i256* %dividend71, i256* %divisor72, i256* %remainder73, i256* %quotient74)
  %success76 = icmp eq i32 %udiv75, 0
  br i1 %success76, label %success77, label %bail78

bail69:                                           ; preds = %endif56
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success77:                                        ; preds = %success68
  %quotient79 = load i256, i256* %quotient74, align 8
  %70 = sub i256 0, %2
  %71 = and i256 %70, %2
  %72 = sub i256 0, %71
  %73 = sub i256 0, %2
  %74 = and i256 %73, %2
  store i256 %72, i256* %dividend80, align 8
  store i256 %74, i256* %divisor81, align 8
  %udiv84 = call i32 @udivmod256(i256* %dividend80, i256* %divisor81, i256* %remainder82, i256* %quotient83)
  %success85 = icmp eq i32 %udiv84, 0
  br i1 %success85, label %success86, label %bail87

bail78:                                           ; preds = %success68
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success86:                                        ; preds = %success77
  %quotient88 = load i256, i256* %quotient83, align 8
  %75 = add i256 %quotient88, 1
  %76 = sub i256 0, %2
  %77 = and i256 %76, %2
  %78 = sub i256 0, %77
  %79 = sub i256 0, %2
  %80 = and i256 %79, %2
  store i256 %78, i256* %dividend89, align 8
  store i256 %80, i256* %divisor90, align 8
  %udiv93 = call i32 @udivmod256(i256* %dividend89, i256* %divisor90, i256* %remainder91, i256* %quotient92)
  %success94 = icmp eq i32 %udiv93, 0
  br i1 %success94, label %success95, label %bail96

bail87:                                           ; preds = %success77
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success95:                                        ; preds = %success86
  %quotient97 = load i256, i256* %quotient92, align 8
  %81 = add i256 %quotient97, 1
  store i256 %prod157, i256* %36, align 8
  store i256 %81, i256* %35, align 8
  %left98 = bitcast i256* %36 to i32*
  %right99 = bitcast i256* %35 to i32*
  %output100 = bitcast i256* %34 to i32*
  call void @__mul32(i32* %left98, i32* %right99, i32* %output100, i32 8)
  %mul101 = load i256, i256* %34, align 8
  %82 = or i256 %quotient79, %mul101
  %83 = sub i256 0, %2
  %84 = and i256 %83, %2
  store i256 %2, i256* %dividend102, align 8
  store i256 %84, i256* %divisor103, align 8
  %udiv106 = call i32 @udivmod256(i256* %dividend102, i256* %divisor103, i256* %remainder104, i256* %quotient105)
  %success107 = icmp eq i32 %udiv106, 0
  br i1 %success107, label %success108, label %bail109

bail96:                                           ; preds = %success86
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success108:                                       ; preds = %success95
  %quotient110 = load i256, i256* %quotient105, align 8
  store i256 3, i256* %33, align 8
  store i256 %quotient110, i256* %32, align 8
  %left111 = bitcast i256* %33 to i32*
  %right112 = bitcast i256* %32 to i32*
  %output113 = bitcast i256* %31 to i32*
  call void @__mul32(i32* %left111, i32* %right112, i32* %output113, i32 8)
  %mul114 = load i256, i256* %31, align 8
  %85 = xor i256 %mul114, 2
  %86 = sub i256 0, %2
  %87 = and i256 %86, %2
  store i256 %2, i256* %dividend115, align 8
  store i256 %87, i256* %divisor116, align 8
  %udiv119 = call i32 @udivmod256(i256* %dividend115, i256* %divisor116, i256* %remainder117, i256* %quotient118)
  %success120 = icmp eq i32 %udiv119, 0
  br i1 %success120, label %success121, label %bail122

bail109:                                          ; preds = %success95
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success121:                                       ; preds = %success108
  %quotient123 = load i256, i256* %quotient118, align 8
  store i256 3, i256* %30, align 8
  store i256 %quotient123, i256* %29, align 8
  %left124 = bitcast i256* %30 to i32*
  %right125 = bitcast i256* %29 to i32*
  %output126 = bitcast i256* %28 to i32*
  call void @__mul32(i32* %left124, i32* %right125, i32* %output126, i32 8)
  %mul127 = load i256, i256* %28, align 8
  %88 = xor i256 %mul127, 2
  %89 = sub i256 0, %2
  %90 = and i256 %89, %2
  store i256 %2, i256* %dividend128, align 8
  store i256 %90, i256* %divisor129, align 8
  %udiv132 = call i32 @udivmod256(i256* %dividend128, i256* %divisor129, i256* %remainder130, i256* %quotient131)
  %success133 = icmp eq i32 %udiv132, 0
  br i1 %success133, label %success134, label %bail135

bail122:                                          ; preds = %success108
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success134:                                       ; preds = %success121
  %quotient136 = load i256, i256* %quotient131, align 8
  %91 = sub i256 0, %2
  %92 = and i256 %91, %2
  store i256 %2, i256* %dividend137, align 8
  store i256 %92, i256* %divisor138, align 8
  %udiv141 = call i32 @udivmod256(i256* %dividend137, i256* %divisor138, i256* %remainder139, i256* %quotient140)
  %success142 = icmp eq i32 %udiv141, 0
  br i1 %success142, label %success143, label %bail144

bail135:                                          ; preds = %success121
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success143:                                       ; preds = %success134
  %quotient145 = load i256, i256* %quotient140, align 8
  store i256 3, i256* %27, align 8
  store i256 %quotient145, i256* %26, align 8
  %left146 = bitcast i256* %27 to i32*
  %right147 = bitcast i256* %26 to i32*
  %output148 = bitcast i256* %25 to i32*
  call void @__mul32(i32* %left146, i32* %right147, i32* %output148, i32 8)
  %mul149 = load i256, i256* %25, align 8
  %93 = xor i256 %mul149, 2
  store i256 %quotient136, i256* %24, align 8
  store i256 %93, i256* %23, align 8
  %left150 = bitcast i256* %24 to i32*
  %right151 = bitcast i256* %23 to i32*
  %output152 = bitcast i256* %22 to i32*
  call void @__mul32(i32* %left150, i32* %right151, i32* %output152, i32 8)
  %mul153 = load i256, i256* %22, align 8
  %94 = sub i256 2, %mul153
  store i256 %88, i256* %21, align 8
  store i256 %94, i256* %20, align 8
  %left154 = bitcast i256* %21 to i32*
  %right155 = bitcast i256* %20 to i32*
  %output156 = bitcast i256* %19 to i32*
  call void @__mul32(i32* %left154, i32* %right155, i32* %output156, i32 8)
  %mul157 = load i256, i256* %19, align 8
  %95 = sub i256 0, %2
  %96 = and i256 %95, %2
  store i256 %2, i256* %dividend158, align 8
  store i256 %96, i256* %divisor159, align 8
  %udiv162 = call i32 @udivmod256(i256* %dividend158, i256* %divisor159, i256* %remainder160, i256* %quotient161)
  %success163 = icmp eq i32 %udiv162, 0
  br i1 %success163, label %success164, label %bail165

bail144:                                          ; preds = %success134
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success164:                                       ; preds = %success143
  %quotient166 = load i256, i256* %quotient161, align 8
  store i256 3, i256* %18, align 8
  store i256 %quotient166, i256* %17, align 8
  %left167 = bitcast i256* %18 to i32*
  %right168 = bitcast i256* %17 to i32*
  %output169 = bitcast i256* %16 to i32*
  call void @__mul32(i32* %left167, i32* %right168, i32* %output169, i32 8)
  %mul170 = load i256, i256* %16, align 8
  %97 = xor i256 %mul170, 2
  %98 = sub i256 0, %2
  %99 = and i256 %98, %2
  store i256 %2, i256* %dividend171, align 8
  store i256 %99, i256* %divisor172, align 8
  %udiv175 = call i32 @udivmod256(i256* %dividend171, i256* %divisor172, i256* %remainder173, i256* %quotient174)
  %success176 = icmp eq i32 %udiv175, 0
  br i1 %success176, label %success177, label %bail178

bail165:                                          ; preds = %success143
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success177:                                       ; preds = %success164
  %quotient179 = load i256, i256* %quotient174, align 8
  %100 = sub i256 0, %2
  %101 = and i256 %100, %2
  store i256 %2, i256* %dividend180, align 8
  store i256 %101, i256* %divisor181, align 8
  %udiv184 = call i32 @udivmod256(i256* %dividend180, i256* %divisor181, i256* %remainder182, i256* %quotient183)
  %success185 = icmp eq i32 %udiv184, 0
  br i1 %success185, label %success186, label %bail187

bail178:                                          ; preds = %success164
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

success186:                                       ; preds = %success177
  %quotient188 = load i256, i256* %quotient183, align 8
  store i256 3, i256* %15, align 8
  store i256 %quotient188, i256* %14, align 8
  %left189 = bitcast i256* %15 to i32*
  %right190 = bitcast i256* %14 to i32*
  %output191 = bitcast i256* %13 to i32*
  call void @__mul32(i32* %left189, i32* %right190, i32* %output191, i32 8)
  %mul192 = load i256, i256* %13, align 8
  %102 = xor i256 %mul192, 2
  store i256 %quotient179, i256* %12, align 8
  store i256 %102, i256* %11, align 8
  %left193 = bitcast i256* %12 to i32*
  %right194 = bitcast i256* %11 to i32*
  %output195 = bitcast i256* %10 to i32*
  call void @__mul32(i32* %left193, i32* %right194, i32* %output195, i32 8)
  %mul196 = load i256, i256* %10, align 8
  %103 = sub i256 2, %mul196
  store i256 %97, i256* %9, align 8
  store i256 %103, i256* %8, align 8
  %left197 = bitcast i256* %9 to i32*
  %right198 = bitcast i256* %8 to i32*
  %output199 = bitcast i256* %7 to i32*
  call void @__mul32(i32* %left197, i32* %right198, i32* %output199, i32 8)
  %mul200 = load i256, i256* %7, align 8
  store i256 %82, i256* %6, align 8
  store i256 %mul200, i256* %5, align 8
  %left201 = bitcast i256* %6 to i32*
  %right202 = bitcast i256* %5 to i32*
  %output203 = bitcast i256* %4 to i32*
  call void @__mul32(i32* %left201, i32* %right202, i32* %output203, i32 8)
  %mul204 = load i256, i256* %4, align 8
  store i256 %mul204, i256* %3, align 8
  ret i32 0

bail187:                                          ; preds = %success177
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable
}

define internal i32 @"FullMath:storage_initializer"() {
entry:
  ret i32 0
}

define internal i32 @"FullMath::FullMath::constructor::861731d5"() {
entry:
  ret i32 0
}

define void @start() {
entry:
  %result27 = alloca i256, align 8
  %stack25 = alloca i256, align 8
  %stack21 = alloca i256, align 8
  %stack17 = alloca i256, align 8
  %result = alloca i256, align 8
  %stack8 = alloca i256, align 8
  %stack4 = alloca i256, align 8
  %stack = alloca i256, align 8
  %value_transferred = alloca i256, align 8
  %0 = bitcast i256* %value_transferred to i8*
  call void @get_msgvalue(i8* %0)
  %value_transferred1 = load i256, i256* %value_transferred, align 8
  %is_value_transfer = icmp ne i256 %value_transferred1, 0
  br i1 %is_value_transfer, label %abort_value_transfer, label %not_value_transfer

not_value_transfer:                               ; preds = %entry
  call void @__init_heap()
  %calldatasize = call i32 @get_call_size()
  store i32 %calldatasize, i32* @calldata_len, align 4
  %1 = call i8* @__malloc(i32 %calldatasize)
  store i8* %1, i8** @calldata_data, align 4
  call void @copy_call_value(i32 0, i32 %calldatasize, i8* %1)
  %2 = bitcast i8* %1 to i32*
  %3 = icmp uge i32 %calldatasize, 4
  br i1 %3, label %switch, label %no_function_matched

abort_value_transfer:                             ; preds = %entry
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

no_function_matched:                              ; preds = %switch, %not_value_transfer
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

switch:                                           ; preds = %not_value_transfer
  %function_selector = load i32, i32* %2, align 4
  store i32 %function_selector, i32* @selector, align 4
  %argsdata = getelementptr i32, i32* %2, i32 1
  %argslen = sub i32 %calldatasize, 4
  switch i32 %function_selector, label %no_function_matched [
    i32 2142435338, label %4
    i32 302619306, label %18
  ]

4:                                                ; preds = %switch
  %data = bitcast i32* %argsdata to i8*
  %data_length = zext i32 %argslen to i64
  %5 = icmp ule i64 32, %data_length
  br i1 %5, label %success, label %bail

success:                                          ; preds = %4
  %6 = getelementptr i8, i8* %data, i64 0
  %7 = bitcast i256* %stack to i8*
  call void @__be32toleN(i8* %6, i8* %7, i32 ptrtoint (i256* getelementptr (i256, i256* null, i32 1) to i32))
  %abi_int256 = load i256, i256* %stack, align 8
  %8 = icmp ule i64 64, %data_length
  br i1 %8, label %success2, label %bail3

bail:                                             ; preds = %4
  ret i32 2

success2:                                         ; preds = %success
  %9 = getelementptr i8, i8* %data, i64 32
  %10 = bitcast i256* %stack4 to i8*
  call void @__be32toleN(i8* %9, i8* %10, i32 ptrtoint (i256* getelementptr (i256, i256* null, i32 1) to i32))
  %abi_int2565 = load i256, i256* %stack4, align 8
  %11 = icmp ule i64 96, %data_length
  br i1 %11, label %success6, label %bail7

bail3:                                            ; preds = %success
  ret i32 2

success6:                                         ; preds = %success2
  %12 = getelementptr i8, i8* %data, i64 64
  %13 = bitcast i256* %stack8 to i8*
  call void @__be32toleN(i8* %12, i8* %13, i32 ptrtoint (i256* getelementptr (i256, i256* null, i32 1) to i32))
  %abi_int2569 = load i256, i256* %stack8, align 8
  %14 = call i32 @"FullMath::FullMath::function::mulDivRoundingUp__uint256_uint256_uint256"(i256 %abi_int256, i256 %abi_int2565, i256 %abi_int2569, i256* %result)
  %success10 = icmp eq i32 %14, 0
  br i1 %success10, label %success11, label %bail12

bail7:                                            ; preds = %success2
  ret i32 2

success11:                                        ; preds = %success6
  %15 = call i8* @__malloc(i32 32)
  call void @__bzero8(i8* %15, i32 4)
  %16 = getelementptr i8, i8* %15, i32 32
  %arg8 = bitcast i256* %result to i8*
  call void @__leNtobe32(i8* %arg8, i8* %15, i32 32)
  %17 = getelementptr i8, i8* %15, i32 32
  call void @set_return(i8* %15, i32 32)
  call void @system_halt(i32 0)
  unreachable

bail12:                                           ; preds = %success6
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable

18:                                               ; preds = %switch
  %data13 = bitcast i32* %argsdata to i8*
  %data_length14 = zext i32 %argslen to i64
  %19 = icmp ule i64 32, %data_length14
  br i1 %19, label %success15, label %bail16

success15:                                        ; preds = %18
  %20 = getelementptr i8, i8* %data13, i64 0
  %21 = bitcast i256* %stack17 to i8*
  call void @__be32toleN(i8* %20, i8* %21, i32 ptrtoint (i256* getelementptr (i256, i256* null, i32 1) to i32))
  %abi_int25618 = load i256, i256* %stack17, align 8
  %22 = icmp ule i64 64, %data_length14
  br i1 %22, label %success19, label %bail20

bail16:                                           ; preds = %18
  ret i32 2

success19:                                        ; preds = %success15
  %23 = getelementptr i8, i8* %data13, i64 32
  %24 = bitcast i256* %stack21 to i8*
  call void @__be32toleN(i8* %23, i8* %24, i32 ptrtoint (i256* getelementptr (i256, i256* null, i32 1) to i32))
  %abi_int25622 = load i256, i256* %stack21, align 8
  %25 = icmp ule i64 96, %data_length14
  br i1 %25, label %success23, label %bail24

bail20:                                           ; preds = %success15
  ret i32 2

success23:                                        ; preds = %success19
  %26 = getelementptr i8, i8* %data13, i64 64
  %27 = bitcast i256* %stack25 to i8*
  call void @__be32toleN(i8* %26, i8* %27, i32 ptrtoint (i256* getelementptr (i256, i256* null, i32 1) to i32))
  %abi_int25626 = load i256, i256* %stack25, align 8
  %28 = call i32 @"FullMath::FullMath::function::mulDiv__uint256_uint256_uint256"(i256 %abi_int25618, i256 %abi_int25622, i256 %abi_int25626, i256* %result27)
  %success28 = icmp eq i32 %28, 0
  br i1 %success28, label %success29, label %bail30

bail24:                                           ; preds = %success19
  ret i32 2

success29:                                        ; preds = %success23
  %29 = call i8* @__malloc(i32 32)
  call void @__bzero8(i8* %29, i32 4)
  %30 = getelementptr i8, i8* %29, i32 32
  %arg831 = bitcast i256* %result27 to i8*
  call void @__leNtobe32(i8* %arg831, i8* %29, i32 32)
  %31 = getelementptr i8, i8* %29, i32 32
  call void @set_return(i8* %29, i32 32)
  call void @system_halt(i32 0)
  unreachable

bail30:                                           ; preds = %success23
  call void @set_return(i8* null, i32 0)
  call void @system_halt(i32 1)
  unreachable
}

attributes #0 = { nofree norecurse nounwind writeonly "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #1 = { nofree norecurse nounwind "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #2 = { nounwind "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #3 = { noinline nounwind "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #4 = { nounwind willreturn }
attributes #5 = { norecurse nounwind readonly "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #6 = { nofree nounwind "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #7 = { nounwind readonly }
attributes #8 = { nofree noinline norecurse nounwind "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #9 = { nounwind readnone speculatable willreturn }
attributes #10 = { norecurse nounwind readnone "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #11 = { nounwind writeonly "correctly-rounded-divide-sqrt-fp-math"="false" "disable-tail-calls"="false" "frame-pointer"="none" "less-precise-fpmad"="false" "min-legal-vector-width"="0" "no-builtins" "no-infs-fp-math"="false" "no-jump-tables"="false" "no-nans-fp-math"="false" "no-signed-zeros-fp-math"="false" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="generic" "unsafe-fp-math"="false" "use-soft-float"="false" }
attributes #12 = { argmemonly nounwind willreturn }
attributes #13 = { noreturn }
attributes #14 = { nobuiltin nounwind "no-builtins" }
attributes #15 = { nobuiltin "no-builtins" }
attributes #16 = { nounwind }

!llvm.ident = !{!0, !0, !1, !1, !0}
!llvm.module.flags = !{!2}

!0 = !{!"clang version 11.0.1 (git://github.com/llvm/llvm-project 13e4369c73355a6c5a31fc1a1115fc7c69743ada)"}
!1 = !{!"clang version 11.0.1 (git://github.com/llvm/llvm-project 852f4d8eb6d317be0947055c0bb6b4fd6c9aa930)"}
!2 = !{i32 1, !"wchar_size", i32 4}
!3 = !{!4, !4, i64 0}
!4 = !{!"long long", !5, i64 0}
!5 = !{!"omnipotent char", !6, i64 0}
!6 = !{!"Simple C/C++ TBAA"}
!7 = !{!5, !5, i64 0}
!8 = !{!9, !9, i64 0}
!9 = !{!"int", !5, i64 0}
!10 = !{!11, !13, i64 12}
!11 = !{!"chunk", !12, i64 0, !12, i64 4, !13, i64 8, !13, i64 12}
!12 = !{!"any pointer", !5, i64 0}
!13 = !{!"long", !5, i64 0}
!14 = !{!11, !13, i64 8}
!15 = !{!11, !12, i64 0}
!16 = !{!11, !12, i64 4}
!17 = !{!18, !18, i64 0}
!18 = !{!"__int128", !5, i64 0}
!19 = !{!20, !20, i64 0}
!20 = !{!"_ExtInt(256)", !5, i64 0}
!21 = !{!22, !22, i64 0}
!22 = !{!"_ExtInt(512)", !5, i64 0}
!23 = !{!24, !25, i64 392}
!24 = !{!"SHA3_CTX", !5, i64 0, !5, i64 200, !25, i64 392}
!25 = !{!"short", !5, i64 0}
