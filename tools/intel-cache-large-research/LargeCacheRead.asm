EXTERN OriginalCacheRead:PROC
.code
LargeCacheRead PROC FRAME
 push rbx
 .pushreg rbx
 push rsi
 .pushreg rsi
 push rdi
 .pushreg rdi
 push r12
 .pushreg r12
 push r13
 .pushreg r13
 sub rsp,20h
 .allocstack 20h
 .endprolog
 mov rbx,rcx
 mov rsi,rdx
 mov rdi,r8
 xor r12d,r12d
 test rdi,rdi
 jz finished
again:
 mov rax,rdi
 sub rax,r12
 mov r8d,10000000h
 cmp rax,r8
 cmovb r8,rax
 mov rcx,rbx
 lea rdx,[rsi+r12]
 call OriginalCacheRead
 test rax,rax
 jz finished
 add r12,rax
 cmp r12,rdi
 jb again
finished:
 mov rax,r12
 add rsp,20h
 pop r13
 pop r12
 pop rdi
 pop rsi
 pop rbx
 ret
LargeCacheRead ENDP
END
